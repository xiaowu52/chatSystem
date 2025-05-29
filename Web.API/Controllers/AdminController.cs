using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Web.API.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography;

namespace Web.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AdminController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;

        public AdminController(AppDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        // 获取管理员令牌
        [HttpPost("token")]
        public async Task<IActionResult> GetAdminToken([FromBody] UserLoginRequest request)
        {
            try
            {
                if (request == null)
                {
                    return BadRequest(new { message = "请求数据不能为空" });
                }

                if (string.IsNullOrEmpty(request.UserName) || string.IsNullOrEmpty(request.Password))
                {
                    return BadRequest(new { message = "用户名和密码不能为空" });
                }

                var admin = await _context.Admins
                    .Include(a => a.User)
                    .FirstOrDefaultAsync(a => a.User.UserName == request.UserName);


                if (!admin.User.IsApproved)
                {
                    return Unauthorized(new { message = "账号未审核通过" });
                }

                if (admin.User.IsBanned)
                {
                    return Unauthorized(new { message = "账号已被禁用" });
                }

                var key = _configuration["Jwt:Key"];
                var issuer = _configuration["Jwt:Issuer"];

                if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(issuer))
                {
                    return StatusCode(500, new { message = "服务器配置错误" });
                }

                var claims = new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, admin.User.Id.ToString()),
                    new Claim(ClaimTypes.Name, admin.User.UserName),
                    new Claim(ClaimTypes.Role, "Admin")
                };

                var tokenHandler = new JwtSecurityTokenHandler();
                var keyBytes = Encoding.UTF8.GetBytes(key);
                var tokenDescriptor = new SecurityTokenDescriptor
                {
                    Subject = new ClaimsIdentity(claims),
                    Expires = DateTime.UtcNow.AddDays(7),
                    Issuer = issuer,
                    SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(keyBytes), SecurityAlgorithms.HmacSha256Signature)
                };

                var token = tokenHandler.CreateToken(tokenDescriptor);
                return Ok(new { token = tokenHandler.WriteToken(token) });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "服务器内部错误", error = ex.Message });
            }
        }

        // 获取所有用户
        [HttpGet("users/all")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetAllUsers()
        {
            try
            {
                var users = await _context.Users
                    .Select(u => new User
                    {
                        Id = u.Id,
                        UserName = u.UserName,
                        DisplayName = u.DisplayName,
                        Email = u.Email,
                        IsApproved = u.IsApproved,
                        IsBanned = u.IsBanned,
                        CreatedAt = u.CreatedAt
                    })
                    .ToListAsync();

                return Ok(users);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "获取用户列表失败", error = ex.Message });
            }
        }

        // 审核用户
        [HttpPost("approve")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ApproveUser([FromBody] UserIdRequest request)
        {
            try
            {
                if (request == null)
                {
                    return BadRequest(new { message = "请求数据不能为空" });
                }

                var user = await _context.Users.FindAsync(request.UserId);
                if (user == null)
                {
                    return NotFound(new { message = "用户不存在" });
                }

                if (user.IsApproved)
                {
                    return BadRequest(new { message = "用户已审核通过" });
                }

                user.IsApproved = true;
                await _context.SaveChangesAsync();
                return Ok(new { message = "审核通过" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "审核失败", error = ex.Message });
            }
        }

        // 禁用用户
        [HttpPost("ban")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> BanUser([FromBody] UserIdRequest request)
        {
            try
            {
                if (request == null)
                {
                    return BadRequest(new { message = "请求数据不能为空" });
                }

                var user = await _context.Users.FindAsync(request.UserId);
                if (user == null)
                {
                    return NotFound(new { message = "用户不存在" });
                }

                if (user.IsBanned)
                {
                    return BadRequest(new { message = "用户已被禁用" });
                }

                user.IsBanned = true;
                await _context.SaveChangesAsync();
                return Ok(new { message = "用户已禁用" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "禁用失败", error = ex.Message });
            }
        }

        // 解禁用户
        [HttpPost("unban")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UnbanUser([FromBody] UserIdRequest request)
        {
            try
            {
                if (request == null)
                {
                    return BadRequest(new { message = "请求数据不能为空" });
                }

                var user = await _context.Users.FindAsync(request.UserId);
                if (user == null)
                {
                    return NotFound(new { message = "用户不存在" });
                }

                if (!user.IsBanned)
                {
                    return BadRequest(new { message = "用户未被禁用" });
                }

                user.IsBanned = false;
                await _context.SaveChangesAsync();
                return Ok(new { message = "用户已解禁" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "解禁失败", error = ex.Message });
            }
        }
        // 查询所有消息
        [HttpGet("messages")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetAllMessages([FromQuery] bool includeDeleted = false)
        {
            try
            {
                var query = _context.Messages.AsQueryable();

                // 根据参数决定是否包括已删除消息
                if (!includeDeleted)
                {
                    query = query.Where(m => !m.IsDeleted);
                }

                var messages = await query
                    .OrderByDescending(m => m.SentAt)
                    .Select(m => new { m.Id, m.SenderId, m.ReceiverId, m.GroupId, m.Content, m.MessageType, m.SentAt, m.IsDeleted })
                    .ToListAsync();
                return Ok(messages);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "获取消息列表失败", error = ex.Message });
            }
        }

        // 删除消息
        [HttpDelete("messages/{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteMessage(int id)
        {
            try
            {
                var msg = await _context.Messages.FindAsync(id);
                if (msg == null)
                {
                    return NotFound(new { message = "消息不存在" });
                }

                if (msg.IsDeleted)
                {
                    return BadRequest(new { message = "消息已被删除" });
                }

                msg.IsDeleted = true;
                await _context.SaveChangesAsync();
                return Ok(new { message = "消息已删除" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "删除消息失败", error = ex.Message });
            }
        }
    }

    public class UserIdRequest
    {
        public int UserId { get; set; }
    }
}
