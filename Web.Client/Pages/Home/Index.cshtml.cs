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
            // ����û��Ƿ��ѵ�¼
            CurrentUser = HttpContext.Session.Get<UserSession>("CurrentUser");
            if (CurrentUser == null)
            {
                return RedirectToPage("/Home/Login");
            }

            var httpClient = _httpClientFactory.CreateClient();
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", HttpContext.Session.GetString("Token"));

            // ���غ����б�
            await LoadFriends(httpClient);

            // ����Ⱥ���б�
            await LoadGroups(httpClient);

            // ����ѡ��ĺ��ѻ�Ⱥ��
            SelectedFriendId = friendId;
            SelectedGroupId = groupId;

            // �����ѡ���ĺ��ѣ����������¼
            if (friendId.HasValue)
            {
                var friend = Friends.FirstOrDefault(f => f.FriendId == friendId.Value);
                if (friend != null)
                {
                    SelectedFriendName = friend.DisplayName ?? friend.UserName;
                    await LoadFriendChatHistory(httpClient, friendId.Value);
                }
            }
            // �����ѡ����Ⱥ�飬����Ⱥ�ļ�¼
            else if (groupId.HasValue)
            {
                var group = Groups.FirstOrDefault(g => g.Id == groupId.Value);
                if (group != null)
                {
                    SelectedGroupName = group.GroupName;
                    await LoadGroupChatHistory(httpClient, groupId.Value);
                }
            }
            // �����ûѡ�񣬵��к��ѣ�Ĭ��ѡ���һ������
            else if (Friends.Any())
            {
                SelectedFriendId = Friends[0].FriendId;
                SelectedFriendName = Friends[0].DisplayName ?? Friends[0].UserName;
                await LoadFriendChatHistory(httpClient, Friends[0].FriendId);
            }
            // ���û�к��ѵ���Ⱥ�飬Ĭ��ѡ���һ��Ⱥ��
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

                // ����˽��
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
                // Ⱥ��
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
                    // ���ͳɹ��������Ϣ�����
                    MessageContent = string.Empty;
                }
                else
                {
                    var errorMessage = await response.Content.ReadAsStringAsync();
                    _logger.LogError($"������Ϣʧ�ܣ�{errorMessage}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"������Ϣʱ��������{ex.Message}");
            }

            // �ض��������ҳ��
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
                    _logger.LogError($"���غ����б�ʧ�ܣ�{response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"���غ����б�ʱ��������{ex.Message}");
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
                    _logger.LogError($"����Ⱥ���б�ʧ�ܣ�{response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"����Ⱥ���б�ʱ��������{ex.Message}");
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

                    // �����Ϣ�Ƿ����Ե�ǰ�û������÷���������
                    foreach (var message in Messages)
                    {
                        message.IsFromCurrentUser = message.SenderId == CurrentUser.UserId;

                        // ���÷���������
                        if (message.SenderId == CurrentUser.UserId)
                        {
                            message.SenderName = CurrentUser.DisplayName ?? CurrentUser.UserName;
                        }
                        else
                        {
                            // ��˽���У�����Ҫ��ʾ���������ƣ����Խ�������Ϊ���ַ���
                            // ��Ϊ��˽���У��Է�ֻ��һ���ˣ�����Ҫ������˭������Ϣ
                            message.SenderName = string.Empty;
                        }
                    }
                }
                else
                {
                    _logger.LogError($"���������¼ʧ�ܣ�{response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"���������¼ʱ��������{ex.Message}");
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

                    // �����Ϣ�Ƿ����Ե�ǰ�û������÷���������
                    foreach (var message in Messages)
                    {
                        message.IsFromCurrentUser = message.SenderId == CurrentUser.UserId;

                        // ���÷���������
                        if (message.SenderId == CurrentUser.UserId)
                        {
                            message.SenderName = CurrentUser.DisplayName ?? CurrentUser.UserName;
                        }
                        else
                        {
                            // ����Ⱥ����Ϣ������Ǻ����б��е��ˣ�ʹ�ú�������
                            var friend = Friends.FirstOrDefault(f => f.FriendId == message.SenderId);
                            if (friend != null)
                            {
                                message.SenderName = friend.DisplayName ?? friend.UserName;
                            }
                            else
                            {
                                // ����ֻ����ʾһ��Ĭ�����ƣ����������Ӧ�ôӷ�������ȡ�û���Ϣ
                                message.SenderName = $"�û�{message.SenderId}";
                            }
                        }
                    }
                }
                else
                {
                    _logger.LogError($"����Ⱥ�ļ�¼ʧ�ܣ�{response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"����Ⱥ�ļ�¼ʱ��������{ex.Message}");
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
        public int? FileId { get; set; }  // ����ļ�ID����
        public DateTime SentAt { get; set; }
        public bool IsDeleted { get; set; }
        public string SenderName { get; set; }  // ��ӷ���������

        [JsonIgnore]
        public bool IsFromCurrentUser { get; set; }

        [JsonIgnore]
        public int CurrentUserId { get; set; }
    }

}
