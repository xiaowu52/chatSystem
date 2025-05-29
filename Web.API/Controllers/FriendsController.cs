using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Web.API.Models;

namespace Web.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class FriendsController : ControllerBase
    {
        private readonly AppDbContext _context;
        public FriendsController(AppDbContext context) => _context = context;

        // 查询用户
        [HttpGet("/api/users/search")]
        public async Task<IActionResult> SearchUsers([FromQuery] string keyword)
        {
            var users = await _context.Users
                .Where(u => u.UserName.Contains(keyword) || u.DisplayName.Contains(keyword))
                .Select(u => new { u.Id, u.UserName, u.DisplayName })
                .ToListAsync();
            return Ok(users);
        }

        // 发送好友申请
        [HttpPost("request")]
        public async Task<IActionResult> RequestFriend([FromBody] FriendRequestModel model)
        {
            if (model.UserId == model.FriendId)
                return BadRequest("不能添加自己为好友");

            // 修改查询条件，只检查活跃的好友关系或等待处理的申请
            if (await _context.Friends.AnyAsync(f =>
                ((f.UserId == model.UserId && f.FriendId == model.FriendId) ||
                 (f.UserId == model.FriendId && f.FriendId == model.UserId)) &&
                (f.Status == "Accepted" || f.Status == "Pending")))
                return BadRequest("已存在好友关系或申请");

            // 检查是否存在已拒绝或已删除的记录，如果有，则更新为待定
            var existingRequest = await _context.Friends.FirstOrDefaultAsync(f =>
                f.UserId == model.UserId && f.FriendId == model.FriendId &&
                (f.Status == "Rejected"));

            if (existingRequest != null)
            {
                // 更新为待定状态
                existingRequest.Status = "Pending";
                existingRequest.RequestedAt = DateTime.UtcNow;
                existingRequest.RespondedAt = null;
                await _context.SaveChangesAsync();
                return Ok(new { message = "好友申请已发送" });
            }

            var friend = new Friend
            {
                UserId = model.UserId,
                FriendId = model.FriendId,
                Status = "Pending",
                RequestedAt = DateTime.UtcNow
            };
            _context.Friends.Add(friend);
            await _context.SaveChangesAsync();
            return Ok(new { message = "好友申请已发送" });
        }

        // 通过用户ID和好友ID删除好友关系
        [HttpDelete("delete")]
        public async Task<IActionResult> DeleteFriendByUserIds([FromQuery] int userId, [FromQuery] int friendId)
        {
            // 查找并删除双向好友关系
            var friendRelation1 = await _context.Friends
                .FirstOrDefaultAsync(f => f.UserId == userId && f.FriendId == friendId);

            var friendRelation2 = await _context.Friends
                .FirstOrDefaultAsync(f => f.UserId == friendId && f.FriendId == userId);

            bool anyDeleted = false;

            if (friendRelation1 != null)
            {
                _context.Friends.Remove(friendRelation1);
                anyDeleted = true;
            }

            if (friendRelation2 != null)
            {
                _context.Friends.Remove(friendRelation2);
                anyDeleted = true;
            }

            if (!anyDeleted)
            {
                return NotFound(new { message = "好友关系不存在" });
            }

            await _context.SaveChangesAsync();
            return Ok(new { message = "好友关系已删除" });
        }


        // 同意/拒绝好友申请
        [HttpPost("respond")]
        public async Task<IActionResult> RespondFriend([FromBody] FriendRespondModel model)
        {
            try
            {
                // 打印调试信息
                Console.WriteLine($"收到好友请求响应: UserId={model.UserId}, FriendId={model.FriendId}, Accept={model.Accept}");

                // 查找待处理的好友请求
                var friend = await _context.Friends.FirstOrDefaultAsync(f =>
                    f.FriendId == model.UserId && f.UserId == model.FriendId && f.Status == "Pending");

                if (friend == null)
                {
                    // 记录错误信息
                    Console.WriteLine("找不到对应的好友请求");
                    return NotFound(new { message = "找不到对应的好友请求" });
                }

                // 更新请求状态
                friend.Status = model.Accept ? "Accepted" : "Rejected";
                friend.RespondedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                // 接受请求时建立双向好友关系
                if (model.Accept)
                {
                    // 检查反向关系是否已存在
                    var existingReverse = await _context.Friends.AnyAsync(f =>
                        f.UserId == model.UserId && f.FriendId == model.FriendId);

                    if (!existingReverse)
                    {
                        var reverse = new Friend
                        {
                            UserId = model.UserId,
                            FriendId = model.FriendId,
                            Status = "Accepted",
                            RequestedAt = DateTime.UtcNow,
                            RespondedAt = DateTime.UtcNow
                        };
                        _context.Friends.Add(reverse);
                        await _context.SaveChangesAsync();
                    }
                }

                return Ok(new { message = model.Accept ? "已同意" : "已拒绝" });
            }
            catch (Exception ex)
            {
                // 记录异常信息
                Console.WriteLine($"处理好友请求响应时出错: {ex.Message}");
                return StatusCode(500, new { message = "服务器内部错误", error = ex.Message });
            }
        }

        // 获取好友关系记录
        [HttpGet("relation")]
        public async Task<IActionResult> GetFriendRelation([FromQuery] int userId, [FromQuery] int friendId)
        {
            var relation = await _context.Friends
                .FirstOrDefaultAsync(f => f.UserId == userId && f.FriendId == friendId && f.Status == "Accepted");

            if (relation == null)
                return NotFound();

            return Ok(relation);
        }


        // 获取好友列表
        [HttpGet("list")]
        public async Task<IActionResult> GetFriends([FromQuery] int userId)
        {
            var friends = await _context.Friends
                .Where(f => f.UserId == userId && f.Status == "Accepted")
                .Include(f => f.FriendUser)
                .Select(f => new { f.FriendId, f.FriendUser.UserName, f.FriendUser.DisplayName })
                .ToListAsync();
            return Ok(friends);
        }

        // 删除好友
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteFriend(int id)
        {
            var friend = await _context.Friends.FindAsync(id);
            if (friend == null) return NotFound();

            // 删除这条记录
            _context.Friends.Remove(friend);

            // 查找并删除反向的好友关系记录
            var reverseFriend = await _context.Friends.FirstOrDefaultAsync(f =>
                f.UserId == friend.FriendId && f.FriendId == friend.UserId);

            if (reverseFriend != null)
            {
                _context.Friends.Remove(reverseFriend);
            }

            await _context.SaveChangesAsync();
            return NoContent();
        }

        // 获取收到的好友请求
        [HttpGet("requests/incoming")]
        public async Task<IActionResult> GetIncomingRequests([FromQuery] int userId)
        {
            var requests = await _context.Friends
                .Where(f => f.FriendId == userId && f.Status == "Pending")
                .Include(f => f.User)
                .Select(f => new
                {
                    Id = f.Id,
                    SenderId = f.UserId,
                    ReceiverId = f.FriendId,
                    SenderUserName = f.User.UserName,
                    RequestedAt = f.RequestedAt,
                    Status = f.Status
                })
                .ToListAsync();

            return Ok(requests);
        }

        // 获取发出的好友请求
        [HttpGet("requests/outgoing")]
        public async Task<IActionResult> GetOutgoingRequests([FromQuery] int userId)
        {
            var requests = await _context.Friends
                .Where(f => f.UserId == userId && (f.Status == "Pending" || f.RespondedAt != null))
                .Include(f => f.FriendUser)
                .Select(f => new
                {
                    Id = f.Id,
                    SenderId = f.UserId,
                    ReceiverId = f.FriendId,
                    ReceiverUserName = f.FriendUser.UserName,
                    RequestedAt = f.RequestedAt,
                    Status = f.Status
                })
                .ToListAsync();

            return Ok(requests);
        }

        // 取消好友请求
        [HttpDelete("requests/{id}")]
        public async Task<IActionResult> CancelFriendRequest(int id)
        {
            var request = await _context.Friends.FindAsync(id);

            if (request == null)
                return NotFound();

            if (request.Status != "Pending")
                return BadRequest("只能取消待处理的好友请求");

            _context.Friends.Remove(request);
            await _context.SaveChangesAsync();

            return Ok(new { message = "请求已取消" });
        }


    }

    public class FriendRequestModel
    {
        public int UserId { get; set; }
        public int FriendId { get; set; }
    }
    public class FriendRespondModel
    {
        public int UserId { get; set; }
        public int FriendId { get; set; }
        public bool Accept { get; set; }
    }
}
