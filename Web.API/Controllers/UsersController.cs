using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Web.API.Models;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Authorization;

namespace Web.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UsersController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;

        public UsersController(AppDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }
        private string GenerateJwtToken(User user, bool isAdmin)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.UserName)
            };
            if (isAdmin)
                claims.Add(new Claim(ClaimTypes.Role, "Admin"));

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: null,
                claims: claims,
                expires: DateTime.Now.AddHours(2),
                signingCredentials: creds);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        // 注册
        [HttpPost("register")]
        public async Task<IActionResult> Register(UserRegisterRequest request)
        {
            if (await _context.Users.AnyAsync(u => u.UserName == request.UserName))
                return BadRequest("用户名已存在");

            var user = new User
            {
                UserName = request.UserName,
                PasswordHash = HashPassword(request.Password),
                DisplayName = request.DisplayName,
                Email = request.Email,
                IsApproved = false, // 需管理员审核
                IsBanned = false,
                CreatedAt = DateTime.UtcNow
            };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();
            return Ok(new { message = "注册成功，请等待管理员审核" });
        }

        // 登录
        [HttpPost("login")]
        public async Task<IActionResult> Login(UserLoginRequest request)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserName == request.UserName);
            if (user == null || !VerifyPassword(request.Password, user.PasswordHash))
                return Unauthorized("用户名或密码错误");
            if (!user.IsApproved)
                return Unauthorized("账号未审核通过");
            if (user.IsBanned)
                return Unauthorized("账号已被禁用");

            var response = new User
            {
                Id = user.Id,
                UserName = user.UserName,
                DisplayName = user.DisplayName,
                Email = user.Email,
                IsApproved = user.IsApproved,
                IsBanned = user.IsBanned,
                CreatedAt = user.CreatedAt
            };
            var isAdmin = await _context.Admins.AnyAsync(a => a.UserId == user.Id);
            var token = GenerateJwtToken(user, isAdmin);
            return Ok(new { token, user = response });
        }

        // 获取用户信息
        [HttpGet("{id}")]
        public async Task<ActionResult<User>> GetUser(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound();

            return new User
            {
                Id = user.Id,
                UserName = user.UserName,
                DisplayName = user.DisplayName,
                Email = user.Email,
                IsApproved = user.IsApproved,
                IsBanned = user.IsBanned,
                CreatedAt = user.CreatedAt
            };
        }

        // 修改用户信息
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateUser(int id, UserRegisterRequest request)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound();

            user.DisplayName = request.DisplayName;
            user.Email = request.Email;
            if (!string.IsNullOrWhiteSpace(request.Password))
                user.PasswordHash = HashPassword(request.Password);

            await _context.SaveChangesAsync();
            return NoContent();
        }

        // 密码加密
        private string HashPassword(string password)
        {
            using var sha256 = SHA256.Create();
            var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
            return Convert.ToBase64String(bytes);
        }

        // 密码校验
        private bool VerifyPassword(string password, string hash)
        {
            return HashPassword(password) == hash;
        }


    }
}
