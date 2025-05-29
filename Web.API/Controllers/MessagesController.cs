using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Web.API.Models;
using Web.API.Hubs;
using Microsoft.AspNetCore.SignalR; 

namespace Web.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class MessagesController : ControllerBase
    {
        private readonly AppDbContext _context;
        public MessagesController(AppDbContext context) => _context = context;

        // 发送消息
        [HttpPost("send")]
        public async Task<IActionResult> SendMessage([FromBody] SendMessageModel model,
    [FromServices] IHubContext<ChatHub> hubContext)
{
            try
            {
                Console.WriteLine($"发送消息: SenderId={model.SenderId}, ReceiverId={model.ReceiverId}, Content={model.Content}");

                var message = new Message
                {
                    SenderId = model.SenderId,
                    ReceiverId = model.ReceiverId,
                    GroupId = model.GroupId,
                    Content = model.Content,
                    MessageType = model.MessageType,
                    FileId = model.FileId,
                    SentAt = DateTime.UtcNow,
                    IsDeleted = false
                };
                _context.Messages.Add(message);
                await _context.SaveChangesAsync();

                if (message.GroupId.HasValue)
                {
                    // 群聊消息
                    string chatId = $"group_{message.GroupId}";
                    var sender = await _context.Users.FindAsync(message.SenderId);
                    string senderName = sender?.DisplayName ?? sender?.UserName ?? "未知用户";
                    await hubContext.Clients.Group(chatId).SendAsync("ReceiveMessage",
                        message.Content, message.SenderId, senderName, DateTime.UtcNow);
                }
                else if (message.ReceiverId.HasValue)
                {
                    // 私聊消息
                    string chatId1 = $"private_{message.SenderId}_{message.ReceiverId}";
                    string chatId2 = $"private_{message.ReceiverId}_{message.SenderId}";
                    var sender = await _context.Users.FindAsync(message.SenderId);
                    string senderName = sender?.DisplayName ?? sender?.UserName ?? "未知用户";

                    await hubContext.Clients.Group(chatId1).SendAsync("ReceiveMessage",
                        message.Content, message.SenderId, senderName, DateTime.UtcNow);
                    await hubContext.Clients.Group(chatId2).SendAsync("ReceiveMessage",
                        message.Content, message.SenderId, senderName, DateTime.UtcNow);
                }

                return Ok(new { message = "消息发送成功", messageId = message.Id });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"发送消息失败: {ex.Message}");
                return StatusCode(500, new { message = "发送消息失败", error = ex.Message });
            }
        }

        // 添加兼容端点，与客户端调用方式匹配
        [HttpGet]
        public async Task<IActionResult> GetMessages([FromQuery] int userId, [FromQuery] int friendId)
        {
            Console.WriteLine($"通过兼容端点获取消息: userId={userId}, friendId={friendId}");
            // 直接调用已有的历史记录获取方法
            return await GetHistory(userId, friendId, null);
        }

        // 获取历史消息
        [HttpGet("history")]
        public async Task<IActionResult> GetHistory(
            [FromQuery] int userId,
            [FromQuery] int? friendId,
            [FromQuery] int? groupId,
            [FromQuery] string startDate = null,
            [FromQuery] string endDate = null)
        {
            try
            {
                Console.WriteLine($"获取消息历史: userId={userId}, friendId={friendId}, groupId={groupId}, startDate={startDate}, endDate={endDate}");

                IQueryable<Message> query = _context.Messages.Where(m => !m.IsDeleted);

                // 根据好友或群组ID过滤
                if (groupId.HasValue)
                {
                    query = query.Where(m => m.GroupId == groupId.Value);
                    Console.WriteLine($"群聊模式: 查询groupId={groupId.Value}的消息");
                }
                else if (friendId.HasValue)
                {
                    query = query.Where(m =>
                        (m.SenderId == userId && m.ReceiverId == friendId) ||
                        (m.SenderId == friendId && m.ReceiverId == userId));
                    Console.WriteLine($"私聊模式: 查询userId={userId}与friendId={friendId.Value}之间的消息");
                }

                // 根据日期范围过滤
                if (!string.IsNullOrEmpty(startDate) && DateTime.TryParse(startDate, out DateTime startDateTime))
                {
                    query = query.Where(m => m.SentAt >= startDateTime.Date);
                    Console.WriteLine($"添加开始日期筛选: {startDateTime.Date}");
                }

                if (!string.IsNullOrEmpty(endDate) && DateTime.TryParse(endDate, out DateTime endDateTime))
                {
                    // 包含当天，所以需要加1天
                    query = query.Where(m => m.SentAt < endDateTime.Date.AddDays(1));
                    Console.WriteLine($"添加结束日期筛选: {endDateTime.Date}");
                }

                var messages = await query
                    .OrderBy(m => m.SentAt)
                    .Select(m => new {
                        m.Id,
                        m.SenderId,
                        m.ReceiverId,
                        m.GroupId,
                        m.Content,
                        m.MessageType,
                        m.FileId,
                        m.SentAt
                    })
                    .ToListAsync();

                Console.WriteLine($"找到{messages.Count}条消息");
                return Ok(messages);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"获取消息历史出错: {ex.Message}");
                return StatusCode(500, new { message = "获取消息历史失败", error = ex.Message });
            }
        }


        // 删除消息
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteMessage(int id)
        {
            try
            {
                var msg = await _context.Messages.FindAsync(id);
                if (msg == null) return NotFound();

                msg.IsDeleted = true;
                await _context.SaveChangesAsync();

                Console.WriteLine($"消息已标记为删除: ID={id}");
                return NoContent();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"删除消息失败: {ex.Message}");
                return StatusCode(500, new { message = "删除消息失败", error = ex.Message });
            }
        }
    }

    public class SendMessageModel
    {
        public int SenderId { get; set; }
        public int? ReceiverId { get; set; }
        public int? GroupId { get; set; }
        public string Content { get; set; }
        public string MessageType { get; set; }
        public int? FileId { get; set; }
    }
}
