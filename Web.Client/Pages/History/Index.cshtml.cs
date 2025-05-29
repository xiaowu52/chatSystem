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

            // ����ѡ�������ʷ��Ϣ
            if (Type == "friend" && SelectedFriendId.HasValue)
            {
                await LoadFriendChatHistory(httpClient, SelectedFriendId.Value);
            }
            else if (Type == "group" && SelectedGroupId.HasValue)
            {
                await LoadGroupChatHistory(httpClient, SelectedGroupId.Value);
            }

            // �����AJAX���󣬷��ز�����ͼ
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return Partial("_MessagesPartial", this);
            }

            return Page();
        }


        public async Task<IActionResult> OnPostDeleteMessageAsync(int messageId)
        {
            // ����û��Ƿ��ѵ�¼
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
                    SuccessMessage = "��Ϣ�ѳɹ�ɾ��";
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    ErrorMessage = $"��Ϣɾ��ʧ��: {response.StatusCode} - {errorContent}";
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"ɾ����Ϣʱ��������: {ex.Message}";
                _logger.LogError(ex, "ɾ����Ϣʱ��������");
            }

            // ���¼���ҳ������
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
                _logger.LogError(ex, "���غ����б�ʱ��������");
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
                _logger.LogError(ex, "����Ⱥ���б�ʱ��������");
            }
        }

        private async Task LoadFriendChatHistory(HttpClient httpClient, int friendId)
        {
            try
            {
                // ��鲢��¼Token
                var token = HttpContext.Session.GetString("Token");
                _logger.LogInformation($"��ȨToken״̬: {(string.IsNullOrEmpty(token) ? "ȱʧ" : "����")}");
                // ����������Ȩͷ
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);


                // ʹ��ApiConfig.Messages.History���BaseUrl/messages
                var response = await httpClient.GetAsync($"{_apiBaseUrl}/messages?userId={CurrentUser.UserId}&friendId={friendId}");

                _logger.LogInformation($"���غ�������API��Ӧ: {response.StatusCode}");

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var messages = JsonSerializer.Deserialize<List<MessageViewModel>>(
                        content,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<MessageViewModel>();

                    Messages = messages.Where(m => !m.IsDeleted).ToList();
                    _logger.LogInformation($"�ɹ�������Ϣ: {Messages.Count}��");

                    // �����Ϣ�Ƿ����Ե�ǰ�û������÷���������
                    foreach (var message in Messages)
                    {
                        message.IsFromCurrentUser = message.SenderId == CurrentUser.UserId;

                        // ���÷���������
                        if (message.IsFromCurrentUser)
                        {
                            message.SenderName = "��";
                        }
                        else
                        {
                            var friend = Friends.FirstOrDefault(f => f.FriendId == message.SenderId);
                            message.SenderName = friend?.DisplayName ?? friend?.UserName ?? $"�û�{message.SenderId}";
                        }
                    }
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError($"���������¼ʧ��: {response.StatusCode} - {errorContent}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"���������¼ʱ��������: {ex.Message}");
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

                    // �����Ϣ�Ƿ����Ե�ǰ�û�������ӷ���������
                    foreach (var message in Messages)
                    {
                        message.IsFromCurrentUser = message.SenderId == CurrentUser.UserId;
                        message.SenderName = message.IsFromCurrentUser ? "��" : message.SenderName ?? "δ֪�û�";
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"����Ⱥ����ʷ��¼ʧ��: {ex.Message}";
                _logger.LogError(ex, "����Ⱥ����ʷ��¼ʧ��");
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
