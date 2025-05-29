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

            // ���ͨ��URL�����ṩ�˽�������Ϣ��Ԥ��ѡ����Ӧ�Ľ��������ͺ�ID
            if (!string.IsNullOrEmpty(ReceiverType))
            {
                // JavaScript��������Ӧ��ѡ��
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
            // ����û��Ƿ��ѵ�¼
            CurrentUser = HttpContext.Session.Get<UserSession>("CurrentUser");
            if (CurrentUser == null)
            {
                return RedirectToPage("/Home/Login");
            }

            var httpClient = _httpClientFactory.CreateClient();
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", HttpContext.Session.GetString("Token"));

            // ���¼��غ��Ѻ�Ⱥ���б��Ա��ڳ���ʱ��ʾ
            await LoadFriends(httpClient);
            await LoadGroups(httpClient);

            try
            {
                if (File == null || File.Length == 0)
                {
                    ErrorMessage = "��ѡ��Ҫ�ϴ����ļ�";
                    return Page();
                }

                // �ϴ��ļ�
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
                    ErrorMessage = $"�ļ��ϴ�ʧ��: {uploadResponse.StatusCode} - {errorContent}";
                    return Page();
                }

                // �ļ��ϴ��ɹ�����ȡ�ļ�ID
                var uploadResult = await uploadResponse.Content.ReadAsStringAsync();
                var fileInfo = JsonSerializer.Deserialize<FileInfoResponse>(
                    uploadResult,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                );

                // ������Ϣ�����ݽ�������ѡ����ѻ�Ⱥ�飩
                object messageRequest;
                if (ReceiverType == "friend" && FriendId.HasValue)
                {
                    messageRequest = new
                    {
                        SenderId = CurrentUser.UserId,
                        ReceiverId = FriendId.Value,
                        Content = $"�������ļ�: {fileInfo.FileName}",
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
                        Content = $"�������ļ�: {fileInfo.FileName}",
                        MessageType = "File",
                        FileId = fileInfo.Id
                    };
                }
                else
                {
                    ErrorMessage = "��ѡ����ȷ�Ľ�����";
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
                    SuccessMessage = "�ļ��ѳɹ��ϴ�������";
                }
                else
                {
                    var errorContent = await messageResponse.Content.ReadAsStringAsync();
                    ErrorMessage = $"�ļ����ϴ�����������Ϣʧ��: {messageResponse.StatusCode} - {errorContent}";
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"�ļ��ϴ�ʱ��������: {ex.Message}";
                _logger.LogError(ex, "�ļ��ϴ�ʱ��������");
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
