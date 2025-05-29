using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace Desktop.Client.Views
{
    public partial class FileTransferDialog : Window
    {
        private readonly HttpClient _httpClient;
        private const string ApiBaseUrl = "https://localhost:5284/api";

        public string FilePath { get; private set; }
        public ObservableCollection<FileTransferItem> FileList { get; private set; }

        public ObservableCollection<FriendViewModel> Friends { get; private set; }
        public ObservableCollection<Group> Groups { get; private set; }

        public bool IsFriendSelected => FriendRadio.IsChecked ?? false;

        public FileTransferDialog()
        {
            InitializeComponent();

            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", App.CurrentUser?.Token);

            FileList = new ObservableCollection<FileTransferItem>();
            FileListView.ItemsSource = FileList;

            Friends = new ObservableCollection<FriendViewModel>();
            Groups = new ObservableCollection<Group>();

            FriendComboBox.ItemsSource = Friends;
            GroupComboBox.ItemsSource = Groups;

            Loaded += FileTransferDialog_Loaded;
        }

        private async void FileTransferDialog_Loaded(object sender, RoutedEventArgs e)
        {
            Mouse.OverrideCursor = Cursors.Wait;
            try
            {
                await LoadFriends();
                await LoadGroups();

                // 默认选中第一项
                if (Friends.Count > 0)
                    FriendComboBox.SelectedIndex = 0;
                if (Groups.Count > 0)
                    GroupComboBox.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载联系人列表失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }

        private async Task LoadFriends()
        {
            var response = await _httpClient.GetAsync($"{ApiBaseUrl}/friends/list?userId={App.CurrentUser.UserId}");
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadAsStringAsync();
                var friendList = JsonSerializer.Deserialize<List<FriendViewModel>>(
                    result, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                Friends.Clear();
                foreach (var friend in friendList)
                {
                    Friends.Add(new FriendViewModel
                    {
                        FriendId = friend.FriendId,
                        UserName = friend.UserName,
                        DisplayName = friend.DisplayName ?? friend.UserName
                    });
                }
            }
        }

        private async Task LoadGroups()
        {
            var response = await _httpClient.GetAsync($"{ApiBaseUrl}/groups/list?userId={App.CurrentUser.UserId}");
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadAsStringAsync();
                var groups = JsonSerializer.Deserialize<List<GroupViewModel>>(result,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                Groups.Clear();
                foreach (var group in groups)
                {
                    Groups.Add(new Group
                    {
                        Id = group.Id,
                        Name = group.GroupName
                    });
                }
            }
        }

        private void ReceiverType_Changed(object sender, RoutedEventArgs e)
        {
            if (FriendRadio == null || FriendComboBox == null || GroupComboBox == null)
                return;
            // 切换接收者类型时更新UI
            if (FriendRadio.IsChecked == true)
            {
                FriendComboBox.Visibility = Visibility.Visible;
                GroupComboBox.Visibility = Visibility.Collapsed;
            }
            else
            {
                FriendComboBox.Visibility = Visibility.Collapsed;
                GroupComboBox.Visibility = Visibility.Visible;
            }
        }

        private void Browse_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog();
            if (dialog.ShowDialog() == true)
            {
                FilePath = dialog.FileName;
                FilePathBox.Text = FilePath;
                var fileInfo = new FileInfo(FilePath);

                // 清除之前的文件（目前只支持一次发送一个文件）
                FileList.Clear();

                FileList.Add(new FileTransferItem
                {
                    FileName = fileInfo.Name,
                    FileSize = FormatFileSize(fileInfo.Length),
                    Status = "待发送"
                });
            }
        }

        private async void Send_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(FilePath))
            {
                MessageBox.Show("请选择要发送的文件", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // 检查是否已选择接收者
            if ((IsFriendSelected && FriendComboBox.SelectedItem == null) ||
                (!IsFriendSelected && GroupComboBox.SelectedItem == null))
            {
                MessageBox.Show("请选择接收方", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                // 更新UI状态
                Mouse.OverrideCursor = Cursors.Wait;
                var fileItem = FileList[0];
                fileItem.Status = "上传中...";

                // 上传文件
                var fileId = await UploadFile(FilePath);
                if (fileId <= 0)
                {
                    fileItem.Status = "上传失败";
                    return;
                }

                // 发送消息
                var fileInfo = new FileInfo(FilePath);
                bool messageSent = false;

                if (IsFriendSelected)
                {
                    var selectedFriend = (FriendViewModel)FriendComboBox.SelectedItem;
                    messageSent = await SendFileMessage(fileId, fileInfo.Name, selectedFriend.FriendId);
                }
                else
                {
                    var selectedGroup = (Group)GroupComboBox.SelectedItem;
                    messageSent = await SendGroupFileMessage(fileId, fileInfo.Name, selectedGroup.Id);
                }

                fileItem.Status = messageSent ? "发送成功" : "发送消息失败";

                // 如果成功，延迟后关闭窗口
                if (messageSent)
                {
                    await Task.Delay(1000);
                    DialogResult = true;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"发送文件时发生错误: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                if (FileList.Count > 0)
                    FileList[0].Status = "发送失败";
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }

        private async Task<int> UploadFile(string filePath)
        {
            try
            {
                // 创建MultipartFormDataContent
                using var fileContent = new MultipartFormDataContent();
                using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
                using var streamContent = new StreamContent(fileStream);

                // 获取MIME类型
                var fileInfo = new FileInfo(filePath);
                string contentType = "application/octet-stream";
                string extension = fileInfo.Extension.ToLower();

                // 简单的MIME类型映射
                if (extension == ".jpg" || extension == ".jpeg") contentType = "image/jpeg";
                else if (extension == ".png") contentType = "image/png";
                else if (extension == ".gif") contentType = "image/gif";
                else if (extension == ".pdf") contentType = "application/pdf";
                else if (extension == ".doc" || extension == ".docx") contentType = "application/msword";
                else if (extension == ".txt") contentType = "text/plain";

                streamContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);

                // 添加文件和上传者ID
                fileContent.Add(streamContent, "file", fileInfo.Name);
                fileContent.Add(new StringContent(App.CurrentUser.UserId.ToString()), "uploadedBy");

                // 发送请求
                var response = await _httpClient.PostAsync($"{ApiBaseUrl}/files/upload", fileContent);

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadAsStringAsync();
                    var fileResponse = JsonSerializer.Deserialize<FileUploadResponse>(
                        result, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    return fileResponse.Id;
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    MessageBox.Show($"文件上传失败: {response.StatusCode} - {errorContent}",
                        "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return 0;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"文件上传过程中发生错误: {ex.Message}",
                    "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return 0;
            }
        }

        private async Task<bool> SendFileMessage(int fileId, string fileName, int receiverId)
        {
            try
            {
                // 构建消息请求
                var request = new
                {
                    SenderId = App.CurrentUser.UserId,
                    ReceiverId = receiverId,
                    Content = $"发送了文件: {fileName}",
                    MessageType = "File",
                    FileId = fileId
                };

                var content = new StringContent(
                    JsonSerializer.Serialize(request),
                    Encoding.UTF8,
                    "application/json"
                );

                var response = await _httpClient.PostAsync($"{ApiBaseUrl}/messages/send", content);

                if (response.IsSuccessStatusCode)
                {
                    return true;
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    MessageBox.Show($"发送文件消息失败: {response.StatusCode} - {errorContent}",
                        "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"发送文件消息时发生错误: {ex.Message}",
                    "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        private async Task<bool> SendGroupFileMessage(int fileId, string fileName, int groupId)
        {
            try
            {
                // 构建消息请求
                var request = new
                {
                    SenderId = App.CurrentUser.UserId,
                    GroupId = groupId,
                    ReceiverId = (int?)null,
                    Content = $"发送了文件: {fileName}",
                    MessageType = "File",
                    FileId = fileId
                };

                var content = new StringContent(
                    JsonSerializer.Serialize(request),
                    Encoding.UTF8,
                    "application/json"
                );

                var response = await _httpClient.PostAsync($"{ApiBaseUrl}/messages/send", content);

                if (response.IsSuccessStatusCode)
                {
                    return true;
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    MessageBox.Show($"发送文件消息失败: {response.StatusCode} - {errorContent}",
                        "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"发送文件消息时发生错误: {ex.Message}",
                    "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void Minimize_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            int order = 0;
            double size = bytes;
            while (size >= 1024 && order < sizes.Length - 1)
            {
                order++;
                size /= 1024;
            }
            return $"{size:0.##} {sizes[order]}";
        }

        // 用于反序列化API响应
        private class FileUploadResponse
        {
            public int Id { get; set; }
            public string FileName { get; set; }
        }

        private class GroupViewModel
        {
            public int Id { get; set; }
            public string GroupName { get; set; }
        }
    }

    public class FileTransferItem
    {
        public string FileName { get; set; }
        public string FileSize { get; set; }
        public string Status { get; set; }
    }
}
