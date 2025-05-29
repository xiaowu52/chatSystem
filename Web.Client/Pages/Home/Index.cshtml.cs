// Index.cshtml.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Web.Client.Config;
using Web.Client.Models;

namespace Web.Client.Pages.Home
{
    public class IndexModel : PageModel
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<IndexModel> _logger;

        public UserSession CurrentUser { get; set; }
        public List<FriendViewModel> Friends { get; set; } = new List<FriendViewModel>();
        public List<GroupViewModel> Groups { get; set; } = new List<GroupViewModel>();
        public List<MessageViewModel> Messages { get; set; } = new List<MessageViewModel>();

        public int? SelectedFriendId { get; set; }
        public string SelectedFriendName { get; set; }
        public int? SelectedGroupId { get; set; }
        public string SelectedGroupName { get; set; }

        [BindProperty]
        public string MessageContent { get; set; }

        public IndexModel(IHttpClientFactory httpClientFactory, ILogger<IndexModel> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public async Task<IActionResult> OnGetAsync(int? friendId = null, int? groupId = null)
        {
            // 检查用户是否已登录
            CurrentUser = HttpContext.Session.Get<UserSession>("CurrentUser");
            if (CurrentUser == null)
            {
                return RedirectToPage("/Home/Login");
            }

            var httpClient = _httpClientFactory.CreateClient();
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", HttpContext.Session.GetString("Token"));

            // 加载好友列表
            await LoadFriends(httpClient);

            // 加载群组列表
            await LoadGroups(httpClient);

            // 处理选择的好友或群组
            SelectedFriendId = friendId;
            SelectedGroupId = groupId;

            // 如果有选定的好友，加载聊天记录
            if (friendId.HasValue)
            {
                var friend = Friends.FirstOrDefault(f => f.FriendId == friendId.Value);
                if (friend != null)
                {
                    SelectedFriendName = friend.DisplayName ?? friend.UserName;
                    await LoadFriendChatHistory(httpClient, friendId.Value);
                }
            }
            // 如果有选定的群组，加载群聊记录
            else if (groupId.HasValue)
            {
                var group = Groups.FirstOrDefault(g => g.Id == groupId.Value);
                if (group != null)
                {
                    SelectedGroupName = group.GroupName;
                    await LoadGroupChatHistory(httpClient, groupId.Value);
                }
            }
            // 如果都没选择，但有好友，默认选择第一个好友
            else if (Friends.Any())
            {
                SelectedFriendId = Friends[0].FriendId;
                SelectedFriendName = Friends[0].DisplayName ?? Friends[0].UserName;
                await LoadFriendChatHistory(httpClient, Friends[0].FriendId);
            }
            // 如果没有好友但有群组，默认选择第一个群组
            else if (Groups.Any())
            {
                SelectedGroupId = Groups[0].Id;
                SelectedGroupName = Groups[0].GroupName;
                await LoadGroupChatHistory(httpClient, Groups[0].Id);
            }

            return Page();
        }

        public async Task<IActionResult> OnPostSendMessageAsync(int? friendId, int? groupId)
        {
            if (string.IsNullOrWhiteSpace(MessageContent))
            {
                return RedirectToPage(new { friendId, groupId });
            }

            CurrentUser = HttpContext.Session.Get<UserSession>("CurrentUser");
            if (CurrentUser == null)
            {
                return RedirectToPage("/Home/Login");
            }

            var httpClient = _httpClientFactory.CreateClient();
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", HttpContext.Session.GetString("Token"));

            try
            {
                object request;

                // 好友私聊
                if (friendId.HasValue)
                {
                    request = new
                    {
                        SenderId = CurrentUser.UserId,
                        ReceiverId = friendId.Value,
                        Content = MessageContent,
                        MessageType = "Text"
                    };
                }
                // 群聊
                else if (groupId.HasValue)
                {
                    request = new
                    {
                        SenderId = CurrentUser.UserId,
                        GroupId = groupId.Value,
                        ReceiverId = (int?)null,
                        Content = MessageContent,
                        MessageType = "Text"
                    };
                }
                else
                {
                    return RedirectToPage();
                }

                var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
                var response = await httpClient.PostAsync(ApiConfig.Messages.Send, content);

                if (response.IsSuccessStatusCode)
                {
                    // 发送成功，清空消息输入框
                    MessageContent = string.Empty;
                }
                else
                {
                    var errorMessage = await response.Content.ReadAsStringAsync();
                    _logger.LogError($"发送消息失败：{errorMessage}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"发送消息时发生错误：{ex.Message}");
            }

            // 重定向回聊天页面
            return RedirectToPage(new { friendId, groupId });
        }

        private async Task LoadFriends(HttpClient httpClient)
        {
            try
            {
                var response = await httpClient.GetAsync($"{ApiConfig.Friends.List}?userId={CurrentUser.UserId}");
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    Friends = JsonSerializer.Deserialize<List<FriendViewModel>>(
                        content,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<FriendViewModel>();
                }
                else
                {
                    _logger.LogError($"加载好友列表失败：{response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"加载好友列表时发生错误：{ex.Message}");
            }
        }

        private async Task LoadGroups(HttpClient httpClient)
        {
            try
            {
                var response = await httpClient.GetAsync($"{ApiConfig.Groups.List}?userId={CurrentUser.UserId}");
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    Groups = JsonSerializer.Deserialize<List<GroupViewModel>>(
                        content,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<GroupViewModel>();
                }
                else
                {
                    _logger.LogError($"加载群组列表失败：{response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"加载群组列表时发生错误：{ex.Message}");
            }
        }

        private async Task LoadFriendChatHistory(HttpClient httpClient, int friendId)
        {
            try
            {
                var response = await httpClient.GetAsync($"{ApiConfig.BaseUrl}/messages?userId={CurrentUser.UserId}&friendId={friendId}");
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var messages = JsonSerializer.Deserialize<List<MessageViewModel>>(
                        content,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<MessageViewModel>();

                    Messages = messages.Where(m => !m.IsDeleted).ToList();

                    // 标记消息是否来自当前用户并设置发送者名称
                    foreach (var message in Messages)
                    {
                        message.IsFromCurrentUser = message.SenderId == CurrentUser.UserId;

                        // 设置发送者名称
                        if (message.SenderId == CurrentUser.UserId)
                        {
                            message.SenderName = CurrentUser.DisplayName ?? CurrentUser.UserName;
                        }
                        else
                        {
                            // 在私聊中，不需要显示发送者名称，所以将其设置为空字符串
                            // 因为在私聊中，对方只有一个人，不需要区分是谁发的消息
                            message.SenderName = string.Empty;
                        }
                    }
                }
                else
                {
                    _logger.LogError($"加载聊天记录失败：{response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"加载聊天记录时发生错误：{ex.Message}");
            }
        }


        private async Task LoadGroupChatHistory(HttpClient httpClient, int groupId)
        {
            try
            {
                var response = await httpClient.GetAsync($"{ApiConfig.Messages.History}?userId={CurrentUser.UserId}&groupId={groupId}");
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var messages = JsonSerializer.Deserialize<List<MessageViewModel>>(
                        content,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<MessageViewModel>();

                    Messages = messages.Where(m => !m.IsDeleted).ToList();

                    // 标记消息是否来自当前用户并设置发送者名称
                    foreach (var message in Messages)
                    {
                        message.IsFromCurrentUser = message.SenderId == CurrentUser.UserId;

                        // 设置发送者名称
                        if (message.SenderId == CurrentUser.UserId)
                        {
                            message.SenderName = CurrentUser.DisplayName ?? CurrentUser.UserName;
                        }
                        else
                        {
                            // 对于群组消息，如果是好友列表中的人，使用好友名称
                            var friend = Friends.FirstOrDefault(f => f.FriendId == message.SenderId);
                            if (friend != null)
                            {
                                message.SenderName = friend.DisplayName ?? friend.UserName;
                            }
                            else
                            {
                                // 否则只能显示一个默认名称，理想情况下应该从服务器获取用户信息
                                message.SenderName = $"用户{message.SenderId}";
                            }
                        }
                    }
                }
                else
                {
                    _logger.LogError($"加载群聊记录失败：{response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"加载群聊记录时发生错误：{ex.Message}");
            }
        }

    }

    public class FriendViewModel
    {
        public int FriendId { get; set; }
        public string UserName { get; set; }
        public string DisplayName { get; set; }
    }

    public class GroupViewModel
    {
        public int Id { get; set; }
        public string GroupName { get; set; }
    }

    public class MessageViewModel
    {
        public int Id { get; set; }
        public int SenderId { get; set; }
        public int? ReceiverId { get; set; }
        public int? GroupId { get; set; }
        public string Content { get; set; }
        public string MessageType { get; set; }
        public int? FileId { get; set; }  // 添加文件ID属性
        public DateTime SentAt { get; set; }
        public bool IsDeleted { get; set; }
        public string SenderName { get; set; }  // 添加发送者名称

        [JsonIgnore]
        public bool IsFromCurrentUser { get; set; }

        [JsonIgnore]
        public int CurrentUserId { get; set; }
    }

}
