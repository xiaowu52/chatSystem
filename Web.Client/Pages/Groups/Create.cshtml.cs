// Groups/Create.cshtml.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Web.Client.Config;
using Web.Client.Models;

namespace Web.Client.Pages.Groups
{
    public class CreateModel : PageModel
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<CreateModel> _logger;

        [BindProperty]
        public string GroupName { get; set; }

        public UserSession CurrentUser { get; set; }
        public string SuccessMessage { get; private set; }
        public string ErrorMessage { get; private set; }

        public CreateModel(IHttpClientFactory httpClientFactory, ILogger<CreateModel> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public IActionResult OnGet()
        {
            // 检查用户是否已登录
            CurrentUser = HttpContext.Session.Get<UserSession>("CurrentUser");
            if (CurrentUser == null)
            {
                return RedirectToPage("/Home/Login");
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            // 检查用户是否已登录
            CurrentUser = HttpContext.Session.Get<UserSession>("CurrentUser");
            if (CurrentUser == null)
            {
                return RedirectToPage("/Home/Login");
            }

            if (string.IsNullOrWhiteSpace(GroupName))
            {
                ErrorMessage = "群聊名称不能为空";
                return Page();
            }

            var httpClient = _httpClientFactory.CreateClient();
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", HttpContext.Session.GetString("Token"));

            try
            {
                var request = new
                {
                    GroupName = GroupName.Trim(),
                    OwnerId = CurrentUser.UserId
                };

                var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
                var response = await httpClient.PostAsync(ApiConfig.Groups.Create, content);

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadAsStringAsync();
                    var groupInfo = JsonSerializer.Deserialize<GroupCreationResponse>(
                        result,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    SuccessMessage = $"群聊 \"{groupInfo.GroupName}\" 创建成功，群ID: {groupInfo.Id}";
                    GroupName = string.Empty; // 清空输入框
                }
                else
                {
                    var errorMessage = await response.Content.ReadAsStringAsync();
                    ErrorMessage = $"创建群聊失败: {errorMessage}";
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"创建群聊时发生错误: {ex.Message}";
            }

            return Page();
        }
    }

    public class GroupCreationResponse
    {
        public int Id { get; set; }
        public string GroupName { get; set; }
    }
}
