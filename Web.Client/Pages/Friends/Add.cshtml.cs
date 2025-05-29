// Friends/Add.cshtml.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Web.Client.Config;
using Web.Client.Models;

namespace Web.Client.Pages.Friends
{
    public class AddModel : PageModel
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<AddModel> _logger;

        public UserSession CurrentUser { get; set; }
        public List<UserViewModel> SearchResults { get; set; } = new List<UserViewModel>();
        public string SearchKeyword { get; set; }

        public string SuccessMessage { get; private set; }
        public string ErrorMessage { get; private set; }

        public AddModel(IHttpClientFactory httpClientFactory, ILogger<AddModel> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public IActionResult OnGet(string keyword = null)
        {
            // ����û��Ƿ��ѵ�¼
            CurrentUser = HttpContext.Session.Get<UserSession>("CurrentUser");
            if (CurrentUser == null)
            {
                return RedirectToPage("/Home/Login");
            }

            SearchKeyword = keyword;
            return Page();
        }

        public async Task<IActionResult> OnGetSearchAsync(string keyword)
        {
            // ����û��Ƿ��ѵ�¼
            CurrentUser = HttpContext.Session.Get<UserSession>("CurrentUser");
            if (CurrentUser == null)
            {
                return RedirectToPage("/Home/Login");
            }

            SearchKeyword = keyword;

            if (string.IsNullOrWhiteSpace(keyword))
            {
                return Page();
            }

            var httpClient = _httpClientFactory.CreateClient();
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", HttpContext.Session.GetString("Token"));

            try
            {
                var response = await httpClient.GetAsync($"{ApiConfig.BaseUrl}/users/search?keyword={Uri.EscapeDataString(keyword)}");
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    SearchResults = JsonSerializer.Deserialize<List<UserViewModel>>(
                        content,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<UserViewModel>();

                    // �Ƴ���ǰ�û��Լ�
                    SearchResults = SearchResults.Where(u => u.Id != CurrentUser.UserId).ToList();
                }
                else
                {
                    ErrorMessage = $"����ʧ�ܣ�{response.StatusCode}";
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"����ʱ��������{ex.Message}";
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAddFriendAsync(int friendId)
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
                var request = new
                {
                    UserId = CurrentUser.UserId,
                    FriendId = friendId
                };

                var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
                var response = await httpClient.PostAsync(ApiConfig.Friends.Request, content);

                if (response.IsSuccessStatusCode)
                {
                    SuccessMessage = "���������ѷ��ͣ��ȴ��Է�ȷ��";
                    // ����������
                    SearchResults.Clear();
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    ErrorMessage = $"���ͺ�������ʧ�ܣ�{errorContent}";
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"���ͺ�������ʱ��������{ex.Message}";
            }

            return Page();
        }
    }

    public class UserViewModel
    {
        public int Id { get; set; }
        public string UserName { get; set; }
        public string DisplayName { get; set; }
    }
}
