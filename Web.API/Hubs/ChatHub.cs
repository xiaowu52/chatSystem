using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

namespace Web.API.Hubs
{
    public class ChatHub : Hub
    {
        // 加入特定聊天（好友聊天或群聊）
        public async Task JoinChat(string chatId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, chatId);
        }

        // 离开特定聊天
        public async Task LeaveChat(string chatId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, chatId);
        }

        // 发送私人消息
        public async Task SendPrivateMessage(string receiverId, string message, int senderId, string senderName)
        {
            // 构建聊天ID（好友聊天）
            string chatId1 = $"private_{senderId}_{receiverId}";
            string chatId2 = $"private_{receiverId}_{senderId}";

            // 发送给相关聊天组
            await Clients.Group(chatId1).SendAsync("ReceiveMessage", message, senderId, senderName, DateTime.UtcNow);
            await Clients.Group(chatId2).SendAsync("ReceiveMessage", message, senderId, senderName, DateTime.UtcNow);
        }

        // 发送群组消息
        public async Task SendGroupMessage(string groupId, string message, int senderId, string senderName)
        {
            string chatId = $"group_{groupId}";
            await Clients.Group(chatId).SendAsync("ReceiveMessage", message, senderId, senderName, DateTime.UtcNow);
        }

    }
}
