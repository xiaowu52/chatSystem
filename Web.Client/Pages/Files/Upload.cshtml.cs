using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Web.Client.Models;

namespace Web.Client.Pages.Files
{
    public class UploadModel : PageModel
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<UploadModel> _logger;
        private readonly string _apiBaseUrl = "https://localhost:5284/api";



        [BindProperty]
        public IFormFile File { get; set; }

        [BindProperty]
        public string ReceiverType { get; set; }

        [BindProperty]
        public int? FriendId { get; set; }

        [BindProperty]
        public int? GroupId { get; set; }

        public UserSession CurrentUser { get; set; }
        public List<FriendViewModel> Friends { get; set; } = new();
        public List<GroupViewModel> Groups { get; set; } = new();

        public string SuccessMessage { get; private set; }
        public string ErrorMessage { get; private set; }

        public UploadModel(IHttpClientFactory httpClientFactory, ILogger<UploadModel> logger)
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

            // 加载好友列表
            await LoadFriends(httpClient);

            // 加载群组列表
            await LoadGroups(httpClient);

            // 如果通过URL参数提供了接收者信息，预先选择相应的接收者类型和ID
            if (!string.IsNullOrEmpty(ReceiverType))
            {
                // JavaScript中设置相应的选项
                ViewData["PreSelectedReceiverType"] = ReceiverType;
                if (ReceiverType == "friend" && FriendId.HasValue)
                {
                    ViewData["PreSelectedFriendId"] = FriendId.Value;
                }
                else if (ReceiverType == "group" && GroupId.HasValue)
                {
                    ViewData["PreSelectedGroupId"] = GroupId.Value;
                }
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

            var httpClient = _httpClientFactory.CreateClient();
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", HttpContext.Session.GetString("Token"));

            // 重新加载好友和群组列表，以便在出错时显示
            await LoadFriends(httpClient);
            await LoadGroups(httpClient);

            try
            {
                if (File == null || File.Length == 0)
                {
                    ErrorMessage = "请选择要上传的文件";
                    return Page();
                }

                // 上传文件
                using var fileContent = new MultipartFormDataContent();
                using var fileStream = File.OpenReadStream();
                using var content = new StreamContent(fileStream);

                content.Headers.ContentType = new MediaTypeHeaderValue(File.ContentType);
                fileContent.Add(content, "file", File.FileName);
                fileContent.Add(new StringContent(CurrentUser.UserId.ToString()), "uploadedBy");

                var uploadResponse = await httpClient.PostAsync($"{_apiBaseUrl}/files/upload", fileContent);

                if (!uploadResponse.IsSuccessStatusCode)
                {
                    var errorContent = await uploadResponse.Content.ReadAsStringAsync();
                    ErrorMessage = $"文件上传失败: {uploadResponse.StatusCode} - {errorContent}";
                    return Page();
                }

                // 文件上传成功，获取文件ID
                var uploadResult = await uploadResponse.Content.ReadAsStringAsync();
                var fileInfo = JsonSerializer.Deserialize<FileInfoResponse>(
                    uploadResult,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                );

                // 发送消息（根据接收类型选择好友或群组）
                object messageRequest;
                if (ReceiverType == "friend" && FriendId.HasValue)
                {
                    messageRequest = new
                    {
                        SenderId = CurrentUser.UserId,
                        ReceiverId = FriendId.Value,
                        Content = $"发送了文件: {fileInfo.FileName}",
                        MessageType = "File",
                        FileId = fileInfo.Id
                    };
                }
                else if (ReceiverType == "group" && GroupId.HasValue)
                {
                    messageRequest = new
                    {
                        SenderId = CurrentUser.UserId,
                        GroupId = GroupId.Value,
                        ReceiverId = (int?)null,
                        Content = $"发送了文件: {fileInfo.FileName}",
                        MessageType = "File",
                        FileId = fileInfo.Id
                    };
                }
                else
                {
                    ErrorMessage = "请选择正确的接收者";
                    return Page();
                }

                var messageContent = new StringContent(
                    JsonSerializer.Serialize(messageRequest),
                    Encoding.UTF8,
                    "application/json"
                );

                var messageResponse = await httpClient.PostAsync($"{_apiBaseUrl}/messages/send", messageContent);

                if (messageResponse.IsSuccessStatusCode)
                {
                    SuccessMessage = "文件已成功上传并发送";
                }
                else
                {
                    var errorContent = await messageResponse.Content.ReadAsStringAsync();
                    ErrorMessage = $"文件已上传，但发送消息失败: {messageResponse.StatusCode} - {errorContent}";
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"文件上传时发生错误: {ex.Message}";
                _logger.LogError(ex, "文件上传时发生错误");
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
                _logger.LogError(ex, "加载好友列表时发生错误");
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
                _logger.LogError(ex, "加载群组列表时发生错误");
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

    public class FileInfoResponse
    {
        public int Id { get; set; }
        public string FileName { get; set; }
    }


}
