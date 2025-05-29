using System;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.AspNetCore.SignalR.Client;
using Newtonsoft.Json.Linq;

namespace Desktop.Client
{
    public class SignalRService
    {
        private HubConnection _connection;
        public event EventHandler<MessageReceivedEventArgs> MessageReceived;

        public bool IsConnected => _connection?.State == HubConnectionState.Connected;

        public async Task InitializeConnection()
        {
            if (_connection != null)
            {
                Console.WriteLine("[DEBUG] 关闭现有SignalR连接");
                await _connection.DisposeAsync();
            }

            var token = App.CurrentUser?.Token;

            if (string.IsNullOrEmpty(token))
            {
                Console.WriteLine("[ERROR] 无法初始化SignalR - 缺少身份验证令牌");
                return;
            }
            string url = $"https://localhost:5284/chatHub?access_token={token}";
            Console.WriteLine($"[DEBUG] 准备连接到SignalR: {url}");
            _connection = new HubConnectionBuilder()
                .WithUrl(url)
                .WithAutomaticReconnect(new[] { TimeSpan.FromSeconds(0), TimeSpan.FromSeconds(2),
                                      TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10) })
                .Build();

            // 添加连接关闭处理
            _connection.Closed += async (error) =>
            {
                Console.WriteLine($"[DEBUG] SignalR连接已关闭: {error?.Message}");
                try
                {
                    await Task.Delay(new Random().Next(0, 5) * 1000);
                    await _connection.StartAsync();
                    Console.WriteLine("[DEBUG] SignalR连接已重新建立");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR] SignalR重连失败: {ex.Message}");
                }
            };

            _connection.On<string, int, string, DateTime>("ReceiveMessage",
                (content, senderId, senderName, sentAt) =>
                {
                    Console.WriteLine($"[DEBUG] ReceiveMessage被调用: content={content}, senderId={senderId}, senderName={senderName}, sentAt={sentAt}");
                    Console.WriteLine($"[DEBUG] 当前连接状态: {_connection.State}, 连接ID: {_connection.ConnectionId}");
                    Console.WriteLine($"[DEBUG] 当前用户ID: {App.CurrentUser?.UserId}");

                    // 在UI线程上触发事件
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        Console.WriteLine($"[DEBUG] 准备触发MessageReceived事件");
                        if (MessageReceived != null)
                        {
                            Console.WriteLine($"[DEBUG] MessageReceived事件有订阅者，正在触发");
                            MessageReceived.Invoke(this, new MessageReceivedEventArgs
                            {
                                Content = content,
                                SenderId = senderId,
                                SenderName = senderName,
                                SentAt = sentAt
                            });
                        }
                        else
                        {
                            Console.WriteLine($"[DEBUG] MessageReceived事件没有订阅者");
                        }
                    });
                });

            try
            {
                Console.WriteLine("[DEBUG] 开始启动SignalR连接...");
                await _connection.StartAsync();
                Console.WriteLine("[DEBUG] SignalR连接已成功建立");
                
                // 打印连接ID和状态
                Console.WriteLine($"[DEBUG] 连接ID: {_connection.ConnectionId}, 状态: {_connection.State}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] SignalR连接错误: {ex.Message}");
                Console.WriteLine($"[ERROR] 异常详情: {ex}");

                // 添加用户通知
                Application.Current.Dispatcher.Invoke(() =>
                {
                    MessageBox.Show($"无法连接到聊天服务器: {ex.Message}",
                        "连接错误", MessageBoxButton.OK, MessageBoxImage.Error);
                });
            }
        }

        // 加入私聊
        public async Task JoinPrivateChat(int friendId)
        {
            if (!IsConnected)
            {
                Console.WriteLine("[ERROR] SignalR未连接，无法加入私聊");
                return;
            }

            try
            {
                int currentUserId = App.CurrentUser.UserId;
                Console.WriteLine($"[DEBUG] 当前用户ID: {currentUserId}, 好友ID: {friendId}");
                
                // 使用与Web.API的ChatHub匹配的聊天组ID格式: private_{senderId}_{receiverId}
                string chatId = $"private_{currentUserId}_{friendId}";
                Console.WriteLine($"[DEBUG] 尝试加入私聊组: {chatId}");
                await _connection.InvokeAsync("JoinChat", chatId);
                Console.WriteLine($"[DEBUG] 成功加入私聊组: {chatId}");
                
                // 同时加入反向的聊天组，确保双向通信
                string reverseChatId = $"private_{friendId}_{currentUserId}";
                Console.WriteLine($"[DEBUG] 尝试加入反向私聊组: {reverseChatId}");
                await _connection.InvokeAsync("JoinChat", reverseChatId);
                Console.WriteLine($"[DEBUG] 成功加入反向私聊组: {reverseChatId}");
                
                Console.WriteLine($"[DEBUG] 已加入私聊: {chatId} 和 {reverseChatId}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] 加入私聊失败: {ex.Message}");
                Console.WriteLine($"[ERROR] 异常详情: {ex}");
            }
        }

        // 加入群聊
        public async Task JoinGroupChat(int groupId)
        {
            if (!IsConnected)
            {
                Console.WriteLine("[ERROR] SignalR未连接，无法加入群聊");
                return;
            }

            try
            {
                // 使用与Web.API的ChatHub匹配的聊天组ID格式: group_{groupId}
                string chatId = $"group_{groupId}";
                Console.WriteLine($"[DEBUG] 尝试加入群聊组: {chatId}");
                await _connection.InvokeAsync("JoinChat", chatId);
                Console.WriteLine($"[DEBUG] 成功加入群聊组: {chatId}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] 加入群聊失败: {ex.Message}");
                Console.WriteLine($"[ERROR] 异常详情: {ex}");
            }
        }

        public async Task JoinChat(string chatId)
        {
            if (IsConnected)
            {
                await _connection.InvokeAsync("JoinChat", chatId);
                Console.WriteLine($"已加入聊天: {chatId}");
            }
        }

        // 离开私聊
        public async Task LeavePrivateChat(int friendId)
        {
            if (!IsConnected)
            {
                Console.WriteLine("[ERROR] SignalR未连接，无法离开私聊");
                return;
            }

            try
            {
                int currentUserId = App.CurrentUser.UserId;
                Console.WriteLine($"[DEBUG] 准备离开私聊，当前用户ID: {currentUserId}, 好友ID: {friendId}");
                
                string chatId = $"private_{currentUserId}_{friendId}";
                Console.WriteLine($"[DEBUG] 尝试离开私聊组: {chatId}");
                await _connection.InvokeAsync("LeaveChat", chatId);
                Console.WriteLine($"[DEBUG] 成功离开私聊组: {chatId}");
                
                string reverseChatId = $"private_{friendId}_{currentUserId}";
                Console.WriteLine($"[DEBUG] 尝试离开反向私聊组: {reverseChatId}");
                await _connection.InvokeAsync("LeaveChat", reverseChatId);
                Console.WriteLine($"[DEBUG] 成功离开反向私聊组: {reverseChatId}");
                
                Console.WriteLine($"[DEBUG] 已离开私聊: {chatId} 和 {reverseChatId}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] 离开私聊失败: {ex.Message}");
                Console.WriteLine($"[ERROR] 异常详情: {ex}");
            }
        }

        // 离开群聊
        public async Task LeaveGroupChat(int groupId)
        {
            if (!IsConnected)
            {
                Console.WriteLine("[ERROR] SignalR未连接，无法离开群聊");
                return;
            }

            try
            {
                string chatId = $"group_{groupId}";
                Console.WriteLine($"[DEBUG] 尝试离开群聊组: {chatId}");
                await _connection.InvokeAsync("LeaveChat", chatId);
                Console.WriteLine($"[DEBUG] 成功离开群聊组: {chatId}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] 离开群聊失败: {ex.Message}");
                Console.WriteLine($"[ERROR] 异常详情: {ex}");
            }
        }

        public async Task LeaveChat(string chatId)
        {
            if (IsConnected)
            {
                await _connection.InvokeAsync("LeaveChat", chatId);
                Console.WriteLine($"已离开聊天: {chatId}");
            }
        }

        public async Task Disconnect()
        {
            if (_connection != null)
            {
                await _connection.StopAsync();
                await _connection.DisposeAsync();
                _connection = null;
            }
        }
        public async Task SendPrivateMessage(int receiverId, string message)
        {
            if (!IsConnected)
            {
                Console.WriteLine("[ERROR] SignalR未连接，无法发送消息");
                return;
            }

            try
            {
                int senderId = App.CurrentUser.UserId;
                string senderName = App.CurrentUser.DisplayName ?? App.CurrentUser.UserName;
                
                Console.WriteLine($"[DEBUG] 准备发送私聊消息，发送者ID: {senderId}, 接收者ID: {receiverId}, 消息内容: {message}");

                await _connection.InvokeAsync("SendPrivateMessage",
                    receiverId.ToString(), message, senderId, senderName);

                Console.WriteLine($"[DEBUG] 已通过SignalR发送私聊消息给用户{receiverId}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] 发送私聊消息失败: {ex.Message}");
                Console.WriteLine($"[ERROR] 异常详情: {ex}");
            }
        }

        public async Task SendGroupMessage(int groupId, string message)
        {
            if (!IsConnected)
            {
                Console.WriteLine("[ERROR] SignalR未连接，无法发送消息");
                return;
            }

            try
            {
                int senderId = App.CurrentUser.UserId;
                string senderName = App.CurrentUser.DisplayName ?? App.CurrentUser.UserName;
                
                Console.WriteLine($"[DEBUG] 准备发送群聊消息，发送者ID: {senderId}, 群组ID: {groupId}, 消息内容: {message}");

                await _connection.InvokeAsync("SendGroupMessage",
                    groupId.ToString(), message, senderId, senderName);

                Console.WriteLine($"[DEBUG] 已通过SignalR发送群聊消息到群{groupId}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] 发送群聊消息失败: {ex.Message}");
                Console.WriteLine($"[ERROR] 异常详情: {ex}");
            }
        }
    }
    public class MessageReceivedEventArgs : EventArgs
    {
        public string Content { get; set; }
        public int SenderId { get; set; }
        public string SenderName { get; set; }
        public DateTime SentAt { get; set; }
    }
}
