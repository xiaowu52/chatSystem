// Login.cshtml.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Web.Client.Config;
using Web.Client.Models;

namespace Web.Client.Pages.Home
{
    public class LoginModel : PageModel
    {
        private readonly IHttpClientFactory _httpClientFactory;

        [BindProperty]
        public string UserName { get; set; }

        [BindProperty]
        public string Password { get; set; }

        public string ErrorMessage { get; private set; }

        public LoginModel(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        public IActionResult OnGet()
        {
            // 如果已登录，重定向到主页
            if (HttpContext.Session.Get<UserSession>("CurrentUser") != null)
            {
                return RedirectToPage("/Home/Index");
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (string.IsNullOrEmpty(UserName) || string.IsNullOrEmpty(Password))
            {
                ErrorMessage = "请输入用户名和密码";
                return Page();
            }

            var httpClient = _httpClientFactory.CreateClient();

            try
            {
                var request = new { UserName, Password };
                var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");

                var response = await httpClient.PostAsync(ApiConfig.Users.Login, content);

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadAsStringAsync();
                    var loginResponse = JsonSerializer.Deserialize<LoginResponse>(
                        result,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                    );

                    // 设置会话
                    var userSession = UserSession.FromLoginResponse(loginResponse);
                    HttpContext.Session.Set("CurrentUser", userSession);
                    HttpContext.Session.SetString("Token", loginResponse.Token);

                    // 返回主页
                    return RedirectToPage("/Home/Index");
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    ErrorMessage = $"登录失败：{errorContent}";
                    return Page();
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"登录失败：{ex.Message}";
                return Page();
            }
        }
    }
}
