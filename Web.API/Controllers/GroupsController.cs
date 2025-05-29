using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Web.API.Models;

namespace Web.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class GroupsController : ControllerBase
    {
        private readonly AppDbContext _context;
        public GroupsController(AppDbContext context) => _context = context;

        // 创建群组
        [HttpPost("create")]
        public async Task<IActionResult> CreateGroup([FromBody] CreateGroupModel model)
        {
            var group = new ChatGroup
            {
                GroupName = model.GroupName,
                OwnerId = model.OwnerId,
                CreatedAt = DateTime.UtcNow
            };
            _context.ChatGroups.Add(group);
            await _context.SaveChangesAsync();

            // 群主自动加入群
            _context.GroupMembers.Add(new GroupMember
            {
                GroupId = group.Id,
                UserId = model.OwnerId,
                JoinedAt = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();

            return Ok(new { group.Id, group.GroupName });
        }

        // 邀请成员
        [HttpPost("members")]
        public async Task<IActionResult> AddMember([FromBody] GroupMemberModel model)
        {
            if (await _context.GroupMembers.AnyAsync(m => m.GroupId == model.GroupId && m.UserId == model.UserId))
                return BadRequest("成员已在群中");
            _context.GroupMembers.Add(new GroupMember
            {
                GroupId = model.GroupId,
                UserId = model.UserId,
                JoinedAt = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();
            return Ok();
        }

        // 移除成员
        [HttpDelete("members")]
        public async Task<IActionResult> RemoveMember([FromQuery] int groupId, [FromQuery] int userId)
        {
            var member = await _context.GroupMembers.FirstOrDefaultAsync(m => m.GroupId == groupId && m.UserId == userId);
            if (member == null) return NotFound();
            _context.GroupMembers.Remove(member);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        // 获取群组列表
        [HttpGet("list")]
        public async Task<IActionResult> GetGroups([FromQuery] int userId)
        {
            var groups = await _context.GroupMembers
                .Where(m => m.UserId == userId)
                .Include(m => m.Group)
                .Select(m => new { m.Group.Id, m.Group.GroupName })
                .ToListAsync();
            return Ok(groups);
        }

        // 获取群成员
        [HttpGet("{id}/members")]
        public async Task<IActionResult> GetGroupMembers(int id)
        {
            var members = await _context.GroupMembers
                .Where(m => m.GroupId == id)
                .Include(m => m.User)
                .Select(m => new { m.UserId, m.User.UserName, m.User.DisplayName })
                .ToListAsync();
            return Ok(members);
        }
    }

    public class CreateGroupModel
    {
        public string GroupName { get; set; }
        public int OwnerId { get; set; }
    }
    public class GroupMemberModel
    {
        public int GroupId { get; set; }
        public int UserId { get; set; }
    }
}
