// Register.cshtml.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Text;
using System.Text.Json;
using Web.Client.Config;
using Web.Client.Models;

namespace Web.Client.Pages.Home
{
    public class RegisterModel : PageModel
    {
        private readonly IHttpClientFactory _httpClientFactory;

        [BindProperty]
        public string UserName { get; set; }

        [BindProperty]
        public string Password { get; set; }

        [BindProperty]
        public string DisplayName { get; set; }

        [BindProperty]
        public string Email { get; set; }

        public string ErrorMessage { get; private set; }
        public string SuccessMessage { get; private set; }

        public RegisterModel(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        public IActionResult OnGet()
        {
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (string.IsNullOrEmpty(UserName) ||
                string.IsNullOrEmpty(Password) ||
                string.IsNullOrEmpty(DisplayName) ||
                string.IsNullOrEmpty(Email))
            {
                ErrorMessage = "�����ֶζ�������д";
                return Page();
            }

            var httpClient = _httpClientFactory.CreateClient();

            try
            {
                var user = new
                {
                    UserName,
                    Password,
                    DisplayName,
                    Email
                };

                var content = new StringContent(
                    JsonSerializer.Serialize(user),
                    Encoding.UTF8,
                    "application/json");

                var response = await httpClient.PostAsync(ApiConfig.Users.Register, content);
                var result = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    SuccessMessage = "ע��ɹ�����ȴ�����Ա��˺��¼";
                    // ��ձ�
                    ModelState.Clear();
                    return Page();
                }
                else
                {
                    try
                    {
                        var errorResponse = JsonSerializer.Deserialize<ErrorResponse>(result);
                        ErrorMessage = errorResponse?.Message ?? "ע��ʧ��";
                    }
                    catch
                    {
                        ErrorMessage = $"ע��ʧ�ܣ�{result}";
                    }
                    return Page();
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"��������{ex.Message}";
                return Page();
            }
        }
    }

    public class ErrorResponse
    {
        public string Message { get; set; }
    }
}
