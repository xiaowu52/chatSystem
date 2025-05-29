using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Web.Client.Models;

namespace Web.Client.Pages.Groups
{
    public class JoinModel : PageModel
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<JoinModel> _logger;
        private readonly string _apiBaseUrl = "https://localhost:5284/api";

        [BindProperty]
        public string GroupId { get; set; }

        public UserSession CurrentUser { get; set; }
        public string SuccessMessage { get; private set; }
        public string ErrorMessage { get; private set; }

        public JoinModel(IHttpClientFactory httpClientFactory, ILogger<JoinModel> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public IActionResult OnGet()
        {
            // ����û��Ƿ��ѵ�¼
            CurrentUser = HttpContext.Session.Get<UserSession>("CurrentUser");
            if (CurrentUser == null)
            {
                return RedirectToPage("/Home/Login");
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            // ����û��Ƿ��ѵ�¼
            CurrentUser = HttpContext.Session.Get<UserSession>("CurrentUser");
            if (CurrentUser == null)
            {
                return RedirectToPage("/Home/Login");
            }

            if (string.IsNullOrWhiteSpace(GroupId))
            {
                ErrorMessage = "������ȺID";
                return Page();
            }

            // ȷ�� GroupId ������
            if (!int.TryParse(GroupId, out int groupIdValue))
            {
                ErrorMessage = "ȺID����������";
                return Page();
            }

            var httpClient = _httpClientFactory.CreateClient();
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", HttpContext.Session.GetString("Token"));

            try
            {
                var request = new
                {
                    GroupId = groupIdValue,
                    UserId = CurrentUser.UserId
                };

                var content = new StringContent(
                    JsonSerializer.Serialize(request),
                    Encoding.UTF8,
                    "application/json"
                );

                var response = await httpClient.PostAsync($"{_apiBaseUrl}/groups/members", content);

                if (response.IsSuccessStatusCode)
                {
                    SuccessMessage = "�ɹ�����Ⱥ��";
                    // ��������
                    GroupId = string.Empty;
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    ErrorMessage = $"����Ⱥ��ʧ��: {response.StatusCode} - {errorContent}";
                    _logger.LogError($"����Ⱥ��ʧ��: {errorContent}");
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"����Ⱥ��ʱ��������: {ex.Message}";
                _logger.LogError(ex, "����Ⱥ��ʱ��������");
            }

            return Page();
        }
    }
}
