using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Net.Http.Headers;
using System.Text.Json;
using Web.Client.Config;
using Web.Client.Models;

namespace Web.Client.Pages.History
{
    public class IndexModel : PageModel
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<IndexModel> _logger;
        private readonly string _apiBaseUrl = "https://localhost:5284/api";

        public UserSession CurrentUser { get; set; }
        public List<FriendViewModel> Friends { get; set; } = new();
        public List<GroupViewModel> Groups { get; set; } = new();
        public List<MessageViewModel> Messages { get; set; } = new();

        [BindProperty(SupportsGet = true)]
        public string Type { get; set; } = "friend";

        [BindProperty(SupportsGet = true)]
        public int? SelectedFriendId { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? SelectedGroupId { get; set; }

        public string SuccessMessage { get; private set; }
        public string ErrorMessage { get; private set; }

        public IndexModel(IHttpClientFactory httpClientFactory, ILogger<IndexModel> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }
        public async Task<IActionResult> OnGetAsync()
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

            // 根据选择加载历史消息
            if (Type == "friend" && SelectedFriendId.HasValue)
            {
                await LoadFriendChatHistory(httpClient, SelectedFriendId.Value);
            }
            else if (Type == "group" && SelectedGroupId.HasValue)
            {
                await LoadGroupChatHistory(httpClient, SelectedGroupId.Value);
            }

            // 如果是AJAX请求，返回部分视图
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return Partial("_MessagesPartial", this);
            }

            return Page();
        }


        public async Task<IActionResult> OnPostDeleteMessageAsync(int messageId)
        {
            // 检查用户是否已登录
            CurrentUser = HttpContext.Session.Get<UserSession>("CurrentUser");
            if (CurrentUser == null)
            {
                return RedirectToPage("/Home/Login");
            }

            var httpClient = _httpClientFactory.CreateClient();
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", HttpContext.Session.GetString("Token"));

            try
            {
                var response = await httpClient.DeleteAsync($"{_apiBaseUrl}/messages/{messageId}");

                if (response.IsSuccessStatusCode)
                {
                    SuccessMessage = "消息已成功删除";
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    ErrorMessage = $"消息删除失败: {response.StatusCode} - {errorContent}";
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"删除消息时发生错误: {ex.Message}";
                _logger.LogError(ex, "删除消息时发生错误");
            }

            // 重新加载页面数据
            await LoadFriends(httpClient);
            await LoadGroups(httpClient);

            if (Type == "friend" && SelectedFriendId.HasValue)
            {
                await LoadFriendChatHistory(httpClient, SelectedFriendId.Value);
            }
            else if (Type == "group" && SelectedGroupId.HasValue)
            {
                await LoadGroupChatHistory(httpClient, SelectedGroupId.Value);
            }

            return Page();
        }

        private async Task LoadFriends(HttpClient httpClient)
        {
            try
            {
                var response = await httpClient.GetAsync($"{_apiBaseUrl}/friends/list?userId={CurrentUser.UserId}");
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    Friends = JsonSerializer.Deserialize<List<FriendViewModel>>(
                        content,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "加载好友列表时发生错误");
            }
        }

        private async Task LoadGroups(HttpClient httpClient)
        {
            try
            {
                var response = await httpClient.GetAsync($"{_apiBaseUrl}/groups/list?userId={CurrentUser.UserId}");
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    Groups = JsonSerializer.Deserialize<List<GroupViewModel>>(
                        content,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "加载群组列表时发生错误");
            }
        }

        private async Task LoadFriendChatHistory(HttpClient httpClient, int friendId)
        {
            try
            {
                // 检查并记录Token
                var token = HttpContext.Session.GetString("Token");
                _logger.LogInformation($"授权Token状态: {(string.IsNullOrEmpty(token) ? "缺失" : "存在")}");
                // 重新设置授权头
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);


                // 使用ApiConfig.Messages.History替代BaseUrl/messages
                var response = await httpClient.GetAsync($"{_apiBaseUrl}/messages?userId={CurrentUser.UserId}&friendId={friendId}");

                _logger.LogInformation($"加载好友聊天API响应: {response.StatusCode}");

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var messages = JsonSerializer.Deserialize<List<MessageViewModel>>(
                        content,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<MessageViewModel>();

                    Messages = messages.Where(m => !m.IsDeleted).ToList();
                    _logger.LogInformation($"成功加载消息: {Messages.Count}条");

                    // 标记消息是否来自当前用户并设置发送者名称
                    foreach (var message in Messages)
                    {
                        message.IsFromCurrentUser = message.SenderId == CurrentUser.UserId;

                        // 设置发送者名称
                        if (message.IsFromCurrentUser)
                        {
                            message.SenderName = "我";
                        }
                        else
                        {
                            var friend = Friends.FirstOrDefault(f => f.FriendId == message.SenderId);
                            message.SenderName = friend?.DisplayName ?? friend?.UserName ?? $"用户{message.SenderId}";
                        }
                    }
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError($"加载聊天记录失败: {response.StatusCode} - {errorContent}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"加载聊天记录时发生错误: {ex.Message}");
            }
        }


        private async Task LoadGroupChatHistory(HttpClient httpClient, int groupId)
        {
            try
            {
                var response = await httpClient.GetAsync($"{_apiBaseUrl}/messages/history?userId={CurrentUser.UserId}&groupId={groupId}");
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var messages = JsonSerializer.Deserialize<List<MessageViewModel>>(
                        content,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();

                    Messages = messages;

                    // 标记消息是否来自当前用户，并添加发送者名称
                    foreach (var message in Messages)
                    {
                        message.IsFromCurrentUser = message.SenderId == CurrentUser.UserId;
                        message.SenderName = message.IsFromCurrentUser ? "我" : message.SenderName ?? "未知用户";
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"加载群聊历史记录失败: {ex.Message}";
                _logger.LogError(ex, "加载群聊历史记录失败");
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
        public DateTime SentAt { get; set; }
        public bool IsDeleted { get; set; }
        public string SenderName { get; set; }

        [System.Text.Json.Serialization.JsonIgnore]
        public bool IsFromCurrentUser { get; set; }
    }
}
