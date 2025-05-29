using System;
using System.Collections.ObjectModel;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace Desktop.Client.Views
{
    public partial class FriendRequestsView : Window
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiBaseUrl = "https://localhost:5284/api";
        private ObservableCollection<FriendRequestViewModel> _incomingRequests;
        private ObservableCollection<FriendRequestViewModel> _outgoingRequests;
        private int _currentUserId;

        public FriendRequestsView(HttpClient httpClient, int currentUserId)
        {
            InitializeComponent();
            _httpClient = httpClient;
            _currentUserId = currentUserId;
            _incomingRequests = new ObservableCollection<FriendRequestViewModel>();
            _outgoingRequests = new ObservableCollection<FriendRequestViewModel>();

            IncomingRequestsList.ItemsSource = _incomingRequests;
            OutgoingRequestsList.ItemsSource = _outgoingRequests;

            LoadFriendRequests();
        }

        private async void LoadFriendRequests()
        {
            await LoadIncomingRequests();
            await LoadOutgoingRequests();
        }

        private async Task LoadIncomingRequests()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_apiBaseUrl}/friends/requests/incoming?userId={App.CurrentUser.UserId}");
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var requests = JsonSerializer.Deserialize<List<FriendRequestDto>>(content,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    _incomingRequests.Clear();
                    foreach (var request in requests)
                    {
                        _incomingRequests.Add(new FriendRequestViewModel
                        {
                            Id = request.Id,
                            SenderId = request.SenderId,
                            ReceiverId = request.ReceiverId,
                            SenderUserName = request.SenderUserName,
                            RequestTime = request.RequestedAt
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载好友请求失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task LoadOutgoingRequests()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_apiBaseUrl}/friends/requests/outgoing?userId={App.CurrentUser.UserId}");
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var requests = JsonSerializer.Deserialize<List<FriendRequestDto>>(content,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    _outgoingRequests.Clear();
                    foreach (var request in requests)
                    {
                        _outgoingRequests.Add(new FriendRequestViewModel
                        {
                            Id = request.Id,
                            ReceiverUserName = request.ReceiverUserName,
                            RequestTime = request.RequestedAt,
                            Status = GetStatusText(request.Status)
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载好友请求失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string GetStatusText(string status)
        {
            return status switch
            {
                "Pending" => "等待回应",
                "Accepted" => "已接受",
                "Rejected" => "已拒绝",
                _ => status
            };
        }

        private async void AcceptRequest_Click(object sender, RoutedEventArgs e)
{
    if (sender is Button button && button.Tag != null)
    {
        try
        {
            int friendRequestId = Convert.ToInt32(button.Tag);

            // 找到对应的好友请求以获取发送者ID
            var request = _incomingRequests.FirstOrDefault(r => r.Id == friendRequestId);
            if (request == null)
            {
                MessageBox.Show("找不到对应的好友请求", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // 构建请求正确的数据
            var responseModel = new
            {
                UserId = App.CurrentUser.UserId, // 当前用户ID (接收者)
                FriendId = request.SenderId, // 发送请求的用户ID
                Accept = true // 接受请求
            };

            // 打印用于调试的信息
            Console.WriteLine($"发送接受好友请求: UserId={responseModel.UserId}, FriendId={responseModel.FriendId}");

            var content = new StringContent(
                JsonSerializer.Serialize(responseModel),
                Encoding.UTF8,
                "application/json");

            // 确保使用正确的API路径
            var response = await _httpClient.PostAsync($"{_apiBaseUrl}/friends/respond", content);

            if (response.IsSuccessStatusCode)
            {
                MessageBox.Show("已接受好友请求", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                await LoadIncomingRequests(); // 重新加载请求列表
            }
            else
            {
                var errorMessage = await response.Content.ReadAsStringAsync();
                MessageBox.Show($"操作失败: {response.StatusCode} - {errorMessage}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"操作失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}



        private async void RejectRequest_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is int requestId)
            {
                try
                {
                    var request = new
                    {
                        UserId = _currentUserId,
                        FriendId = GetSenderIdFromRequest(requestId),
                        Accept = false
                    };

                    var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
                    var response = await _httpClient.PostAsync($"{_apiBaseUrl}/friends/respond", content);

                    if (response.IsSuccessStatusCode)
                    {
                        MessageBox.Show("已拒绝好友请求", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                        LoadFriendRequests();
                    }
                    else
                    {
                        var errorMessage = await response.Content.ReadAsStringAsync();
                        MessageBox.Show($"操作失败: {errorMessage}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"操作失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void CancelRequest_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is int requestId)
            {
                try
                {
                    var response = await _httpClient.DeleteAsync($"{_apiBaseUrl}/friends/requests/{requestId}");

                    if (response.IsSuccessStatusCode)
                    {
                        MessageBox.Show("已取消好友请求", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                        LoadFriendRequests();
                    }
                    else
                    {
                        var errorMessage = await response.Content.ReadAsStringAsync();
                        MessageBox.Show($"操作失败: {errorMessage}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"操作失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        // 这里需要根据请求ID获取发送者ID，实际上我们应该在视图模型中包含此信息
        private int GetSenderIdFromRequest(int requestId)
        {
            var request = _incomingRequests.FirstOrDefault(r => r.Id == requestId);
            return request?.SenderId ?? 0;
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }

    public class FriendRequestViewModel
    {
        public int Id { get; set; }
        public int SenderId { get; set; }
        public int ReceiverId { get; set; }
        public string SenderUserName { get; set; }
        public string ReceiverUserName { get; set; }
        public DateTime RequestTime { get; set; }
        public string Status { get; set; }
    }

    public class FriendRequestDto
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
