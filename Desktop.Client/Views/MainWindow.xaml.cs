using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Input;

namespace Desktop.Client.Views
{
    public partial class MainWindow : Window
    {
        private readonly HttpClient _httpClient;
        private const string ApiBaseUrl = "https://localhost:5284/api";
        private ObservableCollection<FriendViewModel> _friends;
        private ObservableCollection<Group> _groups;
        private ObservableCollection<Message> _messages;

        // 保存当前选择的好友或群组ID
        private int? _currentFriendId;
        private int? _currentGroupId;


        public MainWindow(HttpClient httpClient)
        {
            InitializeComponent();
            _httpClient = httpClient;
            _friends = new ObservableCollection<FriendViewModel>();
            _groups = new ObservableCollection<Group>();
            _messages = new ObservableCollection<Message>();
            FriendsList.ItemsSource = _friends;
            GroupsList.ItemsSource = _groups;
            ChatBox.ItemsSource = _messages;

            App.SignalRService.MessageReceived += OnMessageReceived;


            LoadFriends();
            LoadGroups();

            // 窗口关闭时取消订阅事件
            this.Closing += (s, e) => {
                App.SignalRService.MessageReceived -= OnMessageReceived;
                LeaveCurrentChat();
            };
        }

        // 处理从 SignalR 收到的消息
        private void OnMessageReceived(object sender, MessageReceivedEventArgs e)
        {
            try
            {
                // 检查是否接收到消息事件
                Console.WriteLine($"[DEBUG] 接收到消息事件: {e.Content} 来自 {e.SenderName}({e.SenderId}) 在 {e.SentAt}");
                Console.WriteLine($"[DEBUG] 当前聊天状态: _currentFriendId={_currentFriendId}, _currentGroupId={_currentGroupId}, 当前用户ID={App.CurrentUser.UserId}");

                bool shouldProcess = false;

                // 根据当前聊天上下文确定是否处理消息
                if (_currentFriendId.HasValue)
                {
                    // 私聊消息处理：
                    // 1. 消息是当前好友发送的
                    // 2. 或者是自己发送给当前好友的消息
                    bool isFromCurrentFriend = e.SenderId == _currentFriendId.Value;
                    bool isFromCurrentUser = e.SenderId == App.CurrentUser.UserId;
                    
                    shouldProcess = isFromCurrentFriend || isFromCurrentUser;
                    
                    Console.WriteLine($"[DEBUG] 私聊消息判断: 发送者ID={e.SenderId}, 当前好友ID={_currentFriendId.Value}, 当前用户ID={App.CurrentUser.UserId}");
                    Console.WriteLine($"[DEBUG] 私聊消息判断详情: isFromCurrentFriend={isFromCurrentFriend}, isFromCurrentUser={isFromCurrentUser}");
                    Console.WriteLine($"[DEBUG] 私聊消息判断结果: shouldProcess={shouldProcess}");
                }
                else if (_currentGroupId.HasValue)
                {
                    // 群聊消息处理
                    shouldProcess = true;
                    Console.WriteLine($"[DEBUG] 群聊消息: 当前群组ID={_currentGroupId.Value}, shouldProcess={shouldProcess}");
                }

                Console.WriteLine($"[DEBUG] 最终消息处理判断结果: {shouldProcess}");

                if (shouldProcess)
                {
                    // 在UI线程上添加消息
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        Console.WriteLine($"[DEBUG] 准备在UI线程上添加消息");
                        var message = new Message
                        {
                            SenderId = e.SenderId,
                            Content = e.Content,
                            SentAt = e.SentAt.ToLocalTime(),
                            SenderName = e.SenderName,
                            MessageType = "Text"
                        };

                        Console.WriteLine($"[DEBUG] 显示消息: {e.Content}，来自: {e.SenderName}");
                        _messages.Add(message);

                        // 刷新UI并滚动到最新消息
                        CollectionViewSource.GetDefaultView(_messages).Refresh();
                        if (ChatBox.Items.Count > 0)
                        {
                            ChatBox.ScrollIntoView(ChatBox.Items[ChatBox.Items.Count - 1]);
                        }
                        Console.WriteLine($"[DEBUG] 消息已添加到UI");
                    });
                }
                else
                {
                    Console.WriteLine($"[DEBUG] 消息不属于当前聊天，忽略显示");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] 处理接收消息时出错: {ex.Message}");
                Console.WriteLine($"[ERROR] 异常详情: {ex}");
            }
        }



        // 加入当前选中的聊天
        private async Task JoinCurrentChat()
        {
            Console.WriteLine("[DEBUG] 开始加入当前聊天...");
            Console.WriteLine($"[DEBUG] 当前聊天状态: _currentFriendId={_currentFriendId}, _currentGroupId={_currentGroupId}");

            try
            {
                // 先离开当前聊天（如果有）
                await LeaveCurrentChat();

                if (_currentFriendId.HasValue)
                {
                    Console.WriteLine($"[DEBUG] 准备加入与好友{_currentFriendId.Value}的私聊");
                    // 使用新的JoinPrivateChat方法加入私聊
                    await App.SignalRService.JoinPrivateChat(_currentFriendId.Value);
                    Console.WriteLine($"[DEBUG] 已加入与好友{_currentFriendId.Value}的私聊");
                }
                else if (_currentGroupId.HasValue)
                {
                    Console.WriteLine($"[DEBUG] 准备加入群聊: {_currentGroupId.Value}");
                    // 使用新的JoinGroupChat方法加入群聊
                    await App.SignalRService.JoinGroupChat(_currentGroupId.Value);
                    Console.WriteLine($"[DEBUG] 已加入群聊: {_currentGroupId.Value}");
                }
                else
                {
                    Console.WriteLine("[DEBUG] 没有聊天需要加入，因为没有选中好友或群组");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] 加入聊天时出错: {ex.Message}");
                Console.WriteLine($"[ERROR] 异常详情: {ex}");
                throw; // 重新抛出以便上层处理
            }
        }



        // 离开当前聊天
        private async Task LeaveCurrentChat()
        {
            try
            {
                if (_currentFriendId.HasValue)
                {
                    // 使用新的LeavePrivateChat方法离开私聊
                    await App.SignalRService.LeavePrivateChat(_currentFriendId.Value);
                    Console.WriteLine($"已离开与好友{_currentFriendId.Value}的私聊");
                    _currentFriendId = null;
                }
                else if (_currentGroupId.HasValue)
                {
                    // 使用新的LeaveGroupChat方法离开群聊
                    await App.SignalRService.LeaveGroupChat(_currentGroupId.Value);
                    Console.WriteLine($"已离开群聊: {_currentGroupId.Value}");
                    _currentGroupId = null;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"离开聊天时出错: {ex.Message}");
                // 可以选择不抛出异常，以便在出错时仍然可以尝试加入新聊天
            }
        }


        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // 仅当点击顶部栏时才允许拖动
            if (e.GetPosition(this).Y < 40)  // 假设顶部栏高度为40
            {
                this.DragMove();
            }
        }

        // 修改LoadFriends方法以匹配好友API
        private async void LoadFriends()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{ApiBaseUrl}/friends/list?userId={App.CurrentUser.UserId}");
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadAsStringAsync();
                    var friends = JsonSerializer.Deserialize<List<FriendViewModel>>(
                        result, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    _friends.Clear();
                    foreach (var friend in friends)
                    {
                        _friends.Add(new FriendViewModel
                        {
                            FriendId = friend.FriendId,
                            UserName = friend.UserName,
                            DisplayName = friend.DisplayName
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载好友列表失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void LoadGroups()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{ApiBaseUrl}/groups/list?userId={App.CurrentUser.UserId}");

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadAsStringAsync();
                    // 调整解析代码以匹配 API 返回的结构
                    var groups = JsonSerializer.Deserialize<List<GroupViewModel>>(result,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    _groups.Clear();
                    foreach (var group in groups)
                    {
                        _groups.Add(new Group
                        {
                            Id = group.Id,
                            Name = group.GroupName
                        });
                    }
                    if (_groups.Count > 0)
                    {
                        GroupsList.SelectedItem = _groups[0];
                        GroupsList_SelectionChanged(GroupsList, null);
                    }

                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"获取群组列表失败: {response.StatusCode} - {errorContent}");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载群组列表失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // 添加用于解析群组响应的类
        private class GroupViewModel
        {
            public int Id { get; set; }
            public string GroupName { get; set; }
        }

        private void AddFriend_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new AddFriendDialog(_httpClient, App.CurrentUser.UserId);
            if (dialog.ShowDialog() == true)
            {
                LoadFriends(); // 重新加载好友列表
            }
        }
        // 添加好友请求菜单项点击事件
        private void FriendRequests_Click(object sender, RoutedEventArgs e)
        {
            var requestsWindow = new FriendRequestsView(_httpClient, App.CurrentUser.UserId);
            requestsWindow.Owner = this;
            requestsWindow.ShowDialog();
            LoadFriends(); // 当窗口关闭后重新加载好友列表
        }

        private async void DeleteFriend_Click(object sender, RoutedEventArgs e)
        {
            var selectedFriend = FriendsList.SelectedItem as FriendViewModel;
            if (selectedFriend == null)
            {
                MessageBox.Show("请先选择要删除的好友", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // 确认是否删除
            var result = MessageBox.Show($"确定要删除好友 {selectedFriend.DisplayName ?? selectedFriend.UserName} 吗？",
                "确认删除", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
                return;

            try
            {
                // 直接使用新的API删除双向好友关系
                var response = await _httpClient.DeleteAsync(
                    $"{ApiBaseUrl}/friends/delete?userId={App.CurrentUser.UserId}&friendId={selectedFriend.FriendId}");

                if (response.IsSuccessStatusCode)
                {
                    MessageBox.Show("好友已删除", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                    LoadFriends();  // 重新加载好友列表
                }
                else
                {
                    var errorMessage = await response.Content.ReadAsStringAsync();
                    MessageBox.Show($"删除好友失败: {response.StatusCode} - {errorMessage}",
                        "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"删除好友时发生错误: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        // 添加用于解析好友关系响应的类
        private class FriendRelation
        {
            public int Id { get; set; }
            public int UserId { get; set; }
            public int FriendId { get; set; }
            public string Status { get; set; }
        }


        private async void CreateGroup_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new CreateGroupDialog();
            if (dialog.ShowDialog() == true)
            {
                try
                {
                    // 修改请求对象，包含正确的参数名
                    var request = new
                    {
                        GroupName = dialog.GroupName,
                        OwnerId = App.CurrentUser.UserId  // 添加必要的群主ID参数
                    };

                    var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
                    var response = await _httpClient.PostAsync($"{ApiBaseUrl}/groups/create", content);

                    if (response.IsSuccessStatusCode)
                    {
                        // 成功创建群聊后显示成功消息
                        var result = await response.Content.ReadAsStringAsync();
                        var groupInfo = JsonSerializer.Deserialize<GroupCreationResponse>(result,
                            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                        MessageBox.Show($"群聊 \"{groupInfo.GroupName}\" 创建成功，群ID: {groupInfo.Id}",
                            "创建成功", MessageBoxButton.OK, MessageBoxImage.Information);

                        // 重新加载群组列表
                        LoadGroups();
                    }
                    else
                    {
                        var errorMessage = await response.Content.ReadAsStringAsync();
                        MessageBox.Show($"创建群聊失败: {response.StatusCode} - {errorMessage}",
                            "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"创建群聊时发生错误: {ex.Message}", "错误",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        // 添加用于解析群聊创建响应的类
        private class GroupCreationResponse
        {
            public int Id { get; set; }
            public string GroupName { get; set; }
        }

        private async void JoinGroup_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new JoinGroupDialog();
            if (dialog.ShowDialog() == true)
            {
                try
                {
                    // 需要确保 GroupId 是整数
                    if (!int.TryParse(dialog.GroupId, out int groupId))
                    {
                        MessageBox.Show("群组 ID 必须是数字", "输入错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    var request = new
                    {
                        GroupId = groupId,
                        UserId = App.CurrentUser.UserId
                    };

                    var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
                    var response = await _httpClient.PostAsync($"{ApiBaseUrl}/groups/members", content);

                    if (response.IsSuccessStatusCode)
                    {
                        MessageBox.Show("成功加入群聊", "加入成功", MessageBoxButton.OK, MessageBoxImage.Information);
                        LoadGroups();
                    }
                    else
                    {
                        var errorMessage = await response.Content.ReadAsStringAsync();
                        MessageBox.Show($"加入群聊失败: {response.StatusCode} - {errorMessage}",
                            "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"加入群聊时发生错误: {ex.Message}", "错误",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }


        private void FileTransfer_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new FileTransferDialog();
            dialog.Owner = this;  // 设置所有者窗口
            dialog.ShowDialog();
        }

        private void History_Click(object sender, RoutedEventArgs e)
        {
            // 传递当前用户ID和可选的好友或群组ID
            var dialog = new HistoryDialog(
                App.CurrentUser.UserId,  // 必需的userId参数
                _currentFriendId,        // 可选的当前选中好友ID
                _currentGroupId          // 可选的当前选中群组ID
            );
            dialog.ShowDialog();
        }


        // 添加这个枚举来表示当前聊天类型
        private enum ChatType { None, Friend, Group }
        private ChatType _currentChatType = ChatType.None;

        // 添加这个方法来确保正确切换聊天类型
        private async Task SwitchChatContext(ChatType newChatType)
        {
            if (_currentChatType != newChatType)
            {
                // 先离开当前聊天
                await LeaveCurrentChat();

                // 清空消息
                _messages.Clear();

                // 更新聊天类型
                _currentChatType = newChatType;

                // 确保 UI 更新
                CollectionViewSource.GetDefaultView(_messages).Refresh();
            }
        }


        private async void FriendsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // 如果是取消选择(例如点击空白处)，忽略处理
            if (FriendsList.SelectedItem == null) return;

            var selectedFriend = FriendsList.SelectedItem as FriendViewModel;
            if (selectedFriend != null)
            {
                try
                {
                    // 设置 UI 为"加载中"状态
                    Mouse.OverrideCursor = Cursors.Wait;

                    // 切换聊天上下文
                    await SwitchChatContext(ChatType.Friend);

                    // 确保群组列表取消选择
                    if (GroupsList.SelectedItem != null)
                    {
                        GroupsList.SelectedItem = null;
                    }

                    // 更新UI标识当前正在与谁聊天
                    CurrentChatHeader.Text = $"与 {selectedFriend.DisplayName ?? selectedFriend.UserName} 的聊天";

                    // 保存当前选择的好友ID
                    _currentFriendId = selectedFriend.FriendId;
                    _currentGroupId = null;

                    // 加载聊天记录
                    await LoadChatHistoryAsync(selectedFriend.FriendId);

                    // 加入聊天
                    await JoinCurrentChat();

                    Console.WriteLine($"已切换到与好友 {selectedFriend.UserName} 的聊天");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"切换到私聊时出错: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                finally
                {
                    // 恢复鼠标光标
                    Mouse.OverrideCursor = null;
                }
            }
        }


        private async void GroupsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // 如果是取消选择，忽略处理
            if (GroupsList.SelectedItem == null) return;

            var selectedGroup = GroupsList.SelectedItem as Group;
            if (selectedGroup != null)
            {
                try
                {
                    // 设置 UI 为"加载中"状态
                    Mouse.OverrideCursor = Cursors.Wait;

                    // 切换聊天上下文
                    await SwitchChatContext(ChatType.Group);

                    // 确保好友列表取消选择
                    if (FriendsList.SelectedItem != null)
                    {
                        FriendsList.SelectedItem = null;
                    }

                    // 清空消息框并更新标题
                    _messages.Clear();
                    CurrentChatHeader.Text = $"群聊：{selectedGroup.Name}";

                    // 保存当前选择的群组ID
                    _currentGroupId = selectedGroup.Id;
                    _currentFriendId = null;

                    // 加载群聊历史
                    await LoadGroupChatHistoryAsync(selectedGroup.Id);

                    // 加入聊天
                    await JoinCurrentChat();

                    Console.WriteLine($"已切换到群聊 {selectedGroup.Name}");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"切换到群聊时出错: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                finally
                {
                    // 恢复鼠标光标
                    Mouse.OverrideCursor = null;
                }
            }
        }
        private async Task LoadGroupChatHistoryAsync(int groupId)
        {
            try
            {
                // 修改为使用正确的消息 API 路径，添加 groupId 参数
                var response = await _httpClient.GetAsync($"{ApiBaseUrl}/messages/history?userId={App.CurrentUser.UserId}&groupId={groupId}");

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"群聊记录API返回: {result.Substring(0, Math.Min(100, result.Length))}...");

                    var messages = JsonSerializer.Deserialize<List<Message>>(result,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    // 过滤掉已删除的消息
                    var messageViewModels = messages?
                        .Where(m => !m.IsDeleted)
                        .Select(m => new Message
                        {
                            Id = m.Id,
                            SenderId = m.SenderId,
                            GroupId = m.GroupId,
                            Content = m.Content,
                            SentAt = m.SentAt.ToLocalTime(),
                            MessageType = m.MessageType,
                            IsDeleted = false
                        })
                        .ToList() ?? new List<Message>();

                    // 更新消息集合
                    Application.Current.Dispatcher.Invoke(() => {
                        _messages.Clear();
                        foreach (var msg in messageViewModels)
                        {
                            _messages.Add(msg);
                        }

                        // 确保触发UI更新
                        CollectionViewSource.GetDefaultView(_messages).Refresh();

                        // 滚动到最新消息
                        if (ChatBox.Items.Count > 0)
                        {
                            ChatBox.ScrollIntoView(ChatBox.Items[ChatBox.Items.Count - 1]);
                        }
                    });
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"获取群聊记录失败: {response.StatusCode} - {errorContent}");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载群聊记录失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        private async Task LoadChatHistoryAsync(int friendId)
        {
            try
            {
                var response = await _httpClient.GetAsync($"{ApiBaseUrl}/messages?userId={App.CurrentUser.UserId}&friendId={friendId}");

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"聊天记录API返回: {result.Substring(0, Math.Min(100, result.Length))}...");

                    var messages = JsonSerializer.Deserialize<List<Message>>(result,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    // 使用调度器确保在UI线程上更新集合
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        _messages.Clear();

                        if (messages != null)
                        {
                            foreach (var msg in messages)
                            {
                                // 跳过已删除的消息
                                if (msg.IsDeleted)
                                    continue;

                                // 转换UTC时间到本地时间
                                var localSentTime = msg.SentAt.ToLocalTime();

                                // 添加到集合
                                _messages.Add(new Message
                                {
                                    Id = msg.Id,
                                    SenderId = msg.SenderId,
                                    ReceiverId = msg.ReceiverId,
                                    Content = msg.Content,
                                    SentAt = localSentTime,
                                    MessageType = msg.MessageType,
                                    IsDeleted = false  // 确保设置IsDeleted属性
                                });
                            }

                            // 确保触发UI更新
                            CollectionViewSource.GetDefaultView(_messages).Refresh();

                            // 滚动到最新消息
                            if (ChatBox.Items.Count > 0)
                            {
                                ChatBox.ScrollIntoView(ChatBox.Items[ChatBox.Items.Count - 1]);
                            }
                        }
                    });
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"获取聊天记录失败: {response.StatusCode} - {errorContent}");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载聊天记录失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }



        private async void LoadGroupChatHistory(int groupId)
        {
            var response = await _httpClient.GetAsync($"{ApiBaseUrl}/groups/{groupId}/messages");
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadAsStringAsync();
                var messages = JsonSerializer.Deserialize<List<Message>>(result);
                _messages.Clear();
                foreach (var message in messages)
                {
                    _messages.Add(message);
                }
            }
        }

        private async void SendMessage_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 检查SignalR连接状态
                if (!App.SignalRService.IsConnected)
                {
                    Console.WriteLine("SignalR未连接，尝试重新连接...");
                    await App.InitializeSignalR();

                    if (!App.SignalRService.IsConnected)
                    {
                        MessageBox.Show("实时通信连接失败，消息将发送但可能不会实时显示", "连接警告",
                                       MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
                var selectedFriend = FriendsList.SelectedItem as FriendViewModel;
                var selectedGroup = GroupsList.SelectedItem as Group;

                if (string.IsNullOrWhiteSpace(MessageBox1.Text))
                {
                    return; // 不发送空消息
                }

                string messageContent = MessageBox1.Text;

                if (selectedFriend != null)
                {
                    // 构建消息请求
                    var request = new
                    {
                        SenderId = App.CurrentUser.UserId,
                        ReceiverId = selectedFriend.FriendId,
                        Content = messageContent,
                        MessageType = "Text"
                    };

                    var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
                    var response = await _httpClient.PostAsync($"{ApiBaseUrl}/messages/send", content);

                    if (response.IsSuccessStatusCode)
                    {
                        // 立即在本地显示自己的消息
                        _messages.Add(new Message
                        {
                            SenderId = App.CurrentUser.UserId,
                            ReceiverId = selectedFriend.FriendId,
                            Content = messageContent,
                            SentAt = DateTime.Now,
                            MessageType = "Text",
                            SenderName = App.CurrentUser.DisplayName ?? App.CurrentUser.UserName
                        });

                        // 滚动到最新消息
                        if (ChatBox.Items.Count > 0)
                        {
                            ChatBox.ScrollIntoView(ChatBox.Items[ChatBox.Items.Count - 1]);
                        }

                        // 清空输入框
                        MessageBox1.Text = string.Empty;

                    }
                    else
                    {
                        var errorMessage = await response.Content.ReadAsStringAsync();
                        MessageBox.Show($"消息发送失败: {response.StatusCode} - {errorMessage}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                else if (selectedGroup != null)
                {
                    // 群聊消息发送逻辑
                    var request = new
                    {
                        SenderId = App.CurrentUser.UserId,
                        GroupId = selectedGroup.Id,
                        ReceiverId = (int?)null,
                        Content = messageContent,
                        MessageType = "Text"
                    };

                    var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
                    var response = await _httpClient.PostAsync($"{ApiBaseUrl}/messages/send", content);

                    if (response.IsSuccessStatusCode)
                    {
                        // 立即在本地显示自己的消息
                        _messages.Add(new Message
                        {
                            SenderId = App.CurrentUser.UserId,
                            GroupId = selectedGroup.Id,
                            Content = messageContent,
                            SentAt = DateTime.Now,
                            MessageType = "Text",
                            SenderName = App.CurrentUser.DisplayName ?? App.CurrentUser.UserName
                        });

                        // 滚动到最新消息
                        if (ChatBox.Items.Count > 0)
                        {
                            ChatBox.ScrollIntoView(ChatBox.Items[ChatBox.Items.Count - 1]);
                        }

                        // 清空输入框
                        MessageBox1.Text = string.Empty;

                    }
                    else
                    {
                        var errorMessage = await response.Content.ReadAsStringAsync();
                        MessageBox.Show($"消息发送失败: {response.StatusCode} - {errorMessage}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                else
                {
                    MessageBox.Show("请先选择一个联系人或群组", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"发送消息时发生错误: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public class SenderBackgroundConverter : IValueConverter
        {
            public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
            {
                int senderId = (int)value;
                int currentUserId = App.CurrentUser.UserId;

                return senderId == currentUserId ?
                    new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DCF8C6")) : // 自己发送的消息
                    new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E8F4F5")); // 他人发送的消息
            }

            public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
            {
                throw new NotImplementedException();
            }
        }

        public class SenderAlignmentConverter : IValueConverter
        {
            public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
            {
                int senderId = (int)value;
                int currentUserId = App.CurrentUser.UserId;

                return senderId == currentUserId ? HorizontalAlignment.Right : HorizontalAlignment.Left;
            }

            public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
            {
                throw new NotImplementedException();
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

    public class Friend
    {
        public int Id { get; set; }
        public string UserName { get; set; }
    }

    public class Group
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }

    public class Message
    {
        public int Id { get; set; }
        public int SenderId { get; set; }
        public int? ReceiverId { get; set; }
        public int? GroupId { get; set; }
        public string Content { get; set; }
        public string MessageType { get; set; }
        public DateTime SentAt { get; set; }
        public bool IsDeleted { get; set; }


        // 计算属性，用于确定消息是否来自当前用户
        [JsonIgnore]
        public bool IsFromCurrentUser => SenderId == App.CurrentUser?.UserId;

        // 用于UI显示的额外属性
        [JsonIgnore]
        public string SenderName { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    // 添加消息响应类
    public class MessageResponse
    {
        public int MessageId { get; set; }
        public string Message { get; set; }
    }

    public class FriendViewModel
    {
        public int FriendId { get; set; }
        public string UserName { get; set; }
        public string DisplayName { get; set; }
    }


}
