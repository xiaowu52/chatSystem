using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Net.Http.Headers;
using Web.Client.Models;

namespace Web.Client.Pages.Files
{
    public class DownloadModel : PageModel
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<DownloadModel> _logger;
        private readonly string _apiBaseUrl = "https://localhost:5284/api";

        [BindProperty(SupportsGet = true)]
        public int FileId { get; set; }

        public string DownloadUrl { get; set; }
        public string ErrorMessage { get; set; }

        public DownloadModel(IHttpClientFactory httpClientFactory, ILogger<DownloadModel> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public async Task<IActionResult> OnGet()
        {
            var currentUser = HttpContext.Session.Get<UserSession>("CurrentUser");
            if (currentUser == null)
            {
                return RedirectToPage("/Home/Login");
            }

            try
            {
                if (FileId <= 0)
                {
                    ErrorMessage = "未指定有效的文件ID";
                    return Page();
                }

                // 获取身份验证令牌
                var token = HttpContext.Session.GetString("Token");
                if (string.IsNullOrEmpty(token))
                {
                    ErrorMessage = "无法获取身份验证令牌，请重新登录";
                    return Page();
                }

                // 构建API下载地址
                DownloadUrl = $"{_apiBaseUrl}/files/{FileId}/download?token={Uri.EscapeDataString(token)}";

                _logger.LogInformation($"准备下载文件，ID: {FileId}, URL: {DownloadUrl}");
                return Page();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "准备文件下载时出错");
                ErrorMessage = $"准备文件下载时出现错误: {ex.Message}";
                return Page();
            }
        }
    }
}
