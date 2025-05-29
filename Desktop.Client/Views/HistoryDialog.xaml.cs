using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace Desktop.Client.Views
{
    public partial class HistoryDialog : Window
    {
        private readonly HttpClient _httpClient;
        private const string ApiBaseUrl = "https://localhost:5284/api";
        private int _userId;
        private int? _currentFriendId;
        private int? _currentGroupId;

        private List<FriendViewModel> _friends;
        private List<Group> _groups;

        public HistoryDialog(int userId, int? friendId = null, int? groupId = null)
        {
            InitializeComponent();
            _httpClient = new HttpClient();
            _userId = userId;
            _currentFriendId = friendId;
            _currentGroupId = groupId;

            _friends = new List<FriendViewModel>();
            _groups = new List<Group>();

            // 设置身份验证
            if (!string.IsNullOrEmpty(App.CurrentUser?.Token))
            {
                _httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", App.CurrentUser.Token);
            }

            // 设置默认日期范围
            StartDate.SelectedDate = DateTime.Now.AddDays(-7);
            EndDate.SelectedDate = DateTime.Now;

            // 确保控件已完全加载
            this.Loaded += (s, e) =>
            {
                // 初始化界面状态
                if (_currentGroupId.HasValue)
                {
                    GroupRadio.IsChecked = true;
                }
                else
                {
                    FriendRadio.IsChecked = true;
                }

                // 手动调用一次切换方法，确保UI状态正确
                ChatType_Changed(null, null);

                // 加载好友和群组列表
                LoadFriends();
                LoadGroups();
            };

        }


        private async void LoadFriends()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{ApiBaseUrl}/friends/list?userId={_userId}");

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    _friends = JsonSerializer.Deserialize<List<FriendViewModel>>(content,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    FriendComboBox.ItemsSource = _friends;

                    // 如果有当前选中的好友，设置为默认选中
                    if (_currentFriendId.HasValue)
                    {
                        FriendComboBox.SelectedItem = _friends.FirstOrDefault(f => f.FriendId == _currentFriendId);
                    }
                    else if (_friends.Count > 0)
                    {
                        FriendComboBox.SelectedIndex = 0;
                    }
                }
                else
                {
                    MessageBox.Show($"加载好友列表失败: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载好友列表时出错: {ex.Message}");
            }
        }

        private async void LoadGroups()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{ApiBaseUrl}/groups/list?userId={_userId}");

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var groupViewModels = JsonSerializer.Deserialize<List<GroupViewModel>>(content,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    // 转换为应用中使用的 Group 类
                    _groups = groupViewModels.Select(g => new Group
                    {
                        Id = g.Id,
                        Name = g.GroupName
                    }).ToList();

                    GroupComboBox.ItemsSource = _groups;

                    // 如果有当前选中的群组，设置为默认选中
                    if (_currentGroupId.HasValue)
                    {
                        GroupComboBox.SelectedItem = _groups.FirstOrDefault(g => g.Id == _currentGroupId);
                    }
                    else if (_groups.Count > 0)
                    {
                        GroupComboBox.SelectedIndex = 0;
                    }
                }
                else
                {
                    MessageBox.Show($"加载群组列表失败: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载群组列表时出错: {ex.Message}");
            }
        }

        // 处理聊天类型切换
        private void ChatType_Changed(object sender, RoutedEventArgs e)
        {
            // 确保FriendComboBox和GroupComboBox已初始化
            if (FriendComboBox == null || GroupComboBox == null || SelectionTypeText == null)
            {
                // 记录错误并返回，避免NullReferenceException
                Console.WriteLine("控件尚未初始化");
                return;
            }

            if (FriendRadio.IsChecked == true)
            {
                FriendComboBox.Visibility = Visibility.Visible;
                GroupComboBox.Visibility = Visibility.Collapsed;
                SelectionTypeText.Text = "好友";
                _currentGroupId = null;
            }
            else
            {
                FriendComboBox.Visibility = Visibility.Collapsed;
                GroupComboBox.Visibility = Visibility.Visible;
                SelectionTypeText.Text = "群组";
                _currentFriendId = null;
            }
        }


        private async void Search_Click(object sender, RoutedEventArgs e)
        {
            if (!StartDate.SelectedDate.HasValue || !EndDate.SelectedDate.HasValue)
            {
                MessageBox.Show("请选择开始和结束时间");
                return;
            }

            try
            {
                string url;

                // 根据当前选择的类型获取参数
                if (GroupRadio.IsChecked == true)
                {
                    var selectedGroup = GroupComboBox.SelectedItem as Group;
                    if (selectedGroup == null)
                    {
                        MessageBox.Show("请选择一个群组");
                        return;
                    }

                    url = $"{ApiBaseUrl}/messages/history?userId={_userId}&groupId={selectedGroup.Id}";
                    _currentGroupId = selectedGroup.Id;
                    _currentFriendId = null;
                }
                else
                {
                    var selectedFriend = FriendComboBox.SelectedItem as FriendViewModel;
                    if (selectedFriend == null)
                    {
                        MessageBox.Show("请选择一个好友");
                        return;
                    }

                    url = $"{ApiBaseUrl}/messages/history?userId={_userId}&friendId={selectedFriend.FriendId}";
                    _currentFriendId = selectedFriend.FriendId;
                    _currentGroupId = null;
                }

                // 添加日期范围参数
                string startDateStr = StartDate.SelectedDate.Value.ToString("yyyy-MM-dd");
                string endDateStr = EndDate.SelectedDate.Value.ToString("yyyy-MM-dd");
                url += $"&startDate={startDateStr}&endDate={endDateStr}";

                var response = await _httpClient.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadAsStringAsync();
                    var messages = JsonSerializer.Deserialize<List<MessageViewModel>>(result,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    // 处理发送者名称
                    foreach (var message in messages)
                    {
                        if (message.SenderId == _userId)
                        {
                            message.SenderName = "我";
                        }
                        else if (FriendRadio.IsChecked == true)
                        {
                            var friend = FriendComboBox.SelectedItem as FriendViewModel;
                            message.SenderName = friend?.DisplayName ?? friend?.UserName ?? "未知用户";
                        }
                        else
                        {
                            // 群聊中，可能需要从单独的API获取用户名
                            message.SenderName = $"用户 {message.SenderId}";
                        }
                    }

                    HistoryGrid.ItemsSource = messages;

                    if (messages.Count == 0)
                    {
                        MessageBox.Show("没有找到符合条件的消息记录");
                    }
                }
                else
                {
                    MessageBox.Show($"查询失败: {response.StatusCode} - {await response.Content.ReadAsStringAsync()}");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"查询过程中发生错误: {ex.Message}");
            }
        }

        private async void Delete_Click(object sender, RoutedEventArgs e)
        {
            var selectedMessage = HistoryGrid.SelectedItem as MessageViewModel;
            if (selectedMessage != null)
            {
                // 只允许删除自己的消息
                if (selectedMessage.SenderId != _userId)
                {
                    MessageBox.Show("只能删除自己发送的消息", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                try
                {
                    var response = await _httpClient.DeleteAsync($"{ApiBaseUrl}/messages/{selectedMessage.Id}");
                    if (response.IsSuccessStatusCode)
                    {
                        MessageBox.Show("删除成功");
                        Search_Click(sender, e);  // 重新加载消息
                    }
                    else
                    {
                        MessageBox.Show($"删除失败: {response.StatusCode} - {await response.Content.ReadAsStringAsync()}");
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"删除过程中发生错误: {ex.Message}");
                }
            }
            else
            {
                MessageBox.Show("请先选择要删除的消息");
            }
        }

        private void Minimize_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }

    public class MessageViewModel
    {
        public int Id { get; set; }
        public int SenderId { get; set; }
        public int? ReceiverId { get; set; }
        public int? GroupId { get; set; }
        public string Content { get; set; }
        public string MessageType { get; set; }
        public int? FileId { get; set; }
        public DateTime SentAt { get; set; }
        public string SenderName { get; set; }  // 添加发送者名称属性

        // 添加属性映射以匹配XAML绑定
        public DateTime Timestamp => SentAt;
    }

    // 如果还未定义，需要添加这个类
    public class GroupViewModel
    {
        public int Id { get; set; }
        public string GroupName { get; set; }
    }
}
