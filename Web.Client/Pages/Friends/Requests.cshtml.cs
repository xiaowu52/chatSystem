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
            // 检查用户是否已登录
            CurrentUser = HttpContext.Session.Get<UserSession>("CurrentUser");
            if (CurrentUser == null)
            {
                return RedirectToPage("/Home/Login");
            }

            var httpClient = _httpClientFactory.CreateClient();
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", HttpContext.Session.GetString("Token"));

            // 加载收到的好友请求
            await LoadIncomingRequests(httpClient);

            // 加载发出的好友请求
            await LoadOutgoingRequests(httpClient);

            return Page();
        }

        public async Task<IActionResult> OnPostAcceptAsync(int requestId, int senderId)
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
                // 构建请求正确的数据
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
                    SuccessMessage = "已接受好友请求";
                    // 重新加载好友请求
                    await LoadIncomingRequests(httpClient);
                    await LoadOutgoingRequests(httpClient);
                }
                else
                {
                    var errorMessage = await response.Content.ReadAsStringAsync();
                    ErrorMessage = $"操作失败: {response.StatusCode} - {errorMessage}";
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"操作失败: {ex.Message}";
            }

            return Page();
        }

        public async Task<IActionResult> OnPostRejectAsync(int requestId, int senderId)
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
                    SuccessMessage = "已拒绝好友请求";
                    // 重新加载好友请求
                    await LoadIncomingRequests(httpClient);
                    await LoadOutgoingRequests(httpClient);
                }
                else
                {
                    var errorMessage = await response.Content.ReadAsStringAsync();
                    ErrorMessage = $"操作失败: {errorMessage}";
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"操作失败: {ex.Message}";
            }

            return Page();
        }

        public async Task<IActionResult> OnPostCancelAsync(int requestId)
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
                var response = await httpClient.DeleteAsync($"{ApiConfig.Friends.RequestsIncoming}/{requestId}");

                if (response.IsSuccessStatusCode)
                {
                    SuccessMessage = "已取消好友请求";
                    // 重新加载好友请求
                    await LoadOutgoingRequests(httpClient);
                }
                else
                {
                    var errorMessage = await response.Content.ReadAsStringAsync();
                    ErrorMessage = $"操作失败: {errorMessage}";
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"操作失败: {ex.Message}";
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
                    _logger.LogError($"加载收到的好友请求失败：{response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"加载收到的好友请求时发生错误：{ex.Message}");
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
                    _logger.LogError($"加载发出的好友请求失败：{response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"加载发出的好友请求时发生错误：{ex.Message}");
            }
        }
        public string GetStatusText(string status)
        {
            return status switch
            {
                "Pending" => "等待回应",
                "Accepted" => "已接受",
                "Rejected" => "已拒绝",
                _ => status ?? "未知"
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
