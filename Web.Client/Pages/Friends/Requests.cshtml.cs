// Friends/Requests.cshtml.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Web.Client.Config;
using Web.Client.Models;

namespace Web.Client.Pages.Friends
{
    public class RequestsModel : PageModel
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<RequestsModel> _logger;

        public UserSession CurrentUser { get; set; }
        public List<FriendRequestViewModel> IncomingRequests { get; set; } = new List<FriendRequestViewModel>();
        public List<FriendRequestViewModel> OutgoingRequests { get; set; } = new List<FriendRequestViewModel>();

        public string SuccessMessage { get; private set; }
        public string ErrorMessage { get; private set; }

        public RequestsModel(IHttpClientFactory httpClientFactory, ILogger<RequestsModel> logger)
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

            // �����յ��ĺ�������
            await LoadIncomingRequests(httpClient);

            // ���ط����ĺ�������
            await LoadOutgoingRequests(httpClient);

            return Page();
        }

        public async Task<IActionResult> OnPostAcceptAsync(int requestId, int senderId)
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
                // ����������ȷ������
                var responseModel = new
                {
                    UserId = CurrentUser.UserId,
                    FriendId = senderId,
                    Accept = true
                };

                var content = new StringContent(
                    JsonSerializer.Serialize(responseModel),
                    Encoding.UTF8,
                    "application/json");

                var response = await httpClient.PostAsync(ApiConfig.Friends.Respond, content);

                if (response.IsSuccessStatusCode)
                {
                    SuccessMessage = "�ѽ��ܺ�������";
                    // ���¼��غ�������
                    await LoadIncomingRequests(httpClient);
                    await LoadOutgoingRequests(httpClient);
                }
                else
                {
                    var errorMessage = await response.Content.ReadAsStringAsync();
                    ErrorMessage = $"����ʧ��: {response.StatusCode} - {errorMessage}";
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"����ʧ��: {ex.Message}";
            }

            return Page();
        }

        public async Task<IActionResult> OnPostRejectAsync(int requestId, int senderId)
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
                var requestModel = new
                {
                    UserId = CurrentUser.UserId,
                    FriendId = senderId,
                    Accept = false
                };

                var content = new StringContent(JsonSerializer.Serialize(requestModel), Encoding.UTF8, "application/json");
                var response = await httpClient.PostAsync(ApiConfig.Friends.Respond, content);

                if (response.IsSuccessStatusCode)
                {
                    SuccessMessage = "�Ѿܾ���������";
                    // ���¼��غ�������
                    await LoadIncomingRequests(httpClient);
                    await LoadOutgoingRequests(httpClient);
                }
                else
                {
                    var errorMessage = await response.Content.ReadAsStringAsync();
                    ErrorMessage = $"����ʧ��: {errorMessage}";
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"����ʧ��: {ex.Message}";
            }

            return Page();
        }

        public async Task<IActionResult> OnPostCancelAsync(int requestId)
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
                var response = await httpClient.DeleteAsync($"{ApiConfig.Friends.RequestsIncoming}/{requestId}");

                if (response.IsSuccessStatusCode)
                {
                    SuccessMessage = "��ȡ����������";
                    // ���¼��غ�������
                    await LoadOutgoingRequests(httpClient);
                }
                else
                {
                    var errorMessage = await response.Content.ReadAsStringAsync();
                    ErrorMessage = $"����ʧ��: {errorMessage}";
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"����ʧ��: {ex.Message}";
            }

            return Page();
        }

        private async Task LoadIncomingRequests(HttpClient httpClient)
        {
            try
            {
                var response = await httpClient.GetAsync($"{ApiConfig.Friends.RequestsIncoming}?userId={CurrentUser.UserId}");
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    IncomingRequests = JsonSerializer.Deserialize<List<FriendRequestViewModel>>(
                        content,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<FriendRequestViewModel>();
                }
                else
                {
                    _logger.LogError($"�����յ��ĺ�������ʧ�ܣ�{response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"�����յ��ĺ�������ʱ��������{ex.Message}");
            }
        }

        private async Task LoadOutgoingRequests(HttpClient httpClient)
        {
            try
            {
                var response = await httpClient.GetAsync($"{ApiConfig.Friends.RequestsOutgoing}?userId={CurrentUser.UserId}");
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    OutgoingRequests = JsonSerializer.Deserialize<List<FriendRequestViewModel>>(
                        content,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<FriendRequestViewModel>();
                }
                else
                {
                    _logger.LogError($"���ط����ĺ�������ʧ�ܣ�{response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"���ط����ĺ�������ʱ��������{ex.Message}");
            }
        }
        public string GetStatusText(string status)
        {
            return status switch
            {
                "Pending" => "�ȴ���Ӧ",
                "Accepted" => "�ѽ���",
                "Rejected" => "�Ѿܾ�",
                _ => status ?? "δ֪"
            };
        }

    }

    public class FriendRequestViewModel
    {
        public int Id { get; set; }
        public int SenderId { get; set; }
        public int ReceiverId { get; set; }
        public string SenderUserName { get; set; }
        public string ReceiverUserName { get; set; }
        public DateTime RequestedAt { get; set; }
        public string Status { get; set; }
    }
}
