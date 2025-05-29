using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Web.API.Models;
using Desktop.Admin.Services;
using Desktop.Admin.ViewModels;

namespace Desktop.Admin.Views
{
    public partial class MainWindow : Window
    {
        private readonly ApiService _apiService;
        private ObservableCollection<UserViewModel> _users;
        private ObservableCollection<Message> _messages;

        public MainWindow()
        {
            InitializeComponent();
            _apiService = new ApiService();
            _users = new ObservableCollection<UserViewModel>();
            _messages = new ObservableCollection<Message>();
            UsersGrid.ItemsSource = _users;
            MessagesGrid.ItemsSource = _messages;
            LoadUsers();
            LoadMessages();
        }

        private async void LoadUsers()
        {
            try
            {
                var users = await _apiService.GetAsync<List<User>>("admin/users/all");
                _users.Clear();
                foreach (var user in users)
                {
                    _users.Add(new UserViewModel(user));
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载用户列表失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void LoadMessages()
        {
            try
            {
                // 默认只获取未删除的消息 (includeDeleted=false)
                var messages = await _apiService.GetAsync<List<Message>>("admin/messages?includeDeleted=false");

                // 清空现有消息列表并添加新消息
                _messages.Clear();
                foreach (var message in messages)
                {
                    _messages.Add(message);
                }

                // 刷新视图
                MessagesGrid.ItemsSource = null;
                MessagesGrid.ItemsSource = _messages;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载消息列表失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void ApproveUser_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var userViewModel = button?.DataContext as UserViewModel;

            if (userViewModel == null)
            {
                MessageBox.Show("无法获取用户数据", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                await _apiService.ApproveUser(userViewModel.Id);
                MessageBox.Show("审核通过", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                LoadUsers();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"审核失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BanUser_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var userViewModel = button?.DataContext as UserViewModel;

            if (userViewModel == null)
            {
                MessageBox.Show("无法获取用户数据", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            try
            {
                await _apiService.BanUser(userViewModel.Id);
                MessageBox.Show("用户已禁用", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                LoadUsers();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"禁用失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void UnbanUser_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var userViewModel = button?.DataContext as UserViewModel;

            if (userViewModel == null)
            {
                MessageBox.Show("无法获取用户数据", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            
            try
            {
                await _apiService.UnbanUser(userViewModel.Id);
                MessageBox.Show("用户已解禁", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                LoadUsers();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"解禁失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void DeleteMessage_Click(object sender, RoutedEventArgs e)
        {
            var selectedMessage = MessagesGrid.SelectedItem as Message;
            if (selectedMessage == null)
            {
                MessageBox.Show("请选择要删除的消息", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                await _apiService.DeleteMessage(selectedMessage.Id);
                MessageBox.Show("消息已删除", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                LoadMessages();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"删除失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RefreshUsers_Click(object sender, RoutedEventArgs e)
        {
            LoadUsers();
        }

        private void RefreshMessages_Click(object sender, RoutedEventArgs e)
        {
            LoadMessages();
        }

        private void UserSearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var searchText = UserSearchBox.Text.ToLower();
            var filteredUsers = _users.Where(u => 
                u.UserName.ToLower().Contains(searchText) || 
                u.DisplayName.ToLower().Contains(searchText) || 
                u.Email.ToLower().Contains(searchText));
            UsersGrid.ItemsSource = filteredUsers;
        }

        private void MessageSearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var searchText = MessageSearchBox.Text.ToLower();
            var filteredMessages = _messages.Where(m => 
                m.Content.ToLower().Contains(searchText));
            MessagesGrid.ItemsSource = filteredMessages;
        }

        private void UserFilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedItem = UserFilterComboBox.SelectedItem as ComboBoxItem;
            if (selectedItem == null) return;

            var filter = selectedItem.Content.ToString();
            var filteredUsers = filter switch
            {
                "全部" => _users,
                "待审核" => _users.Where(u => !u.IsApproved && !u.IsBanned),
                "已审核" => _users.Where(u => u.IsApproved && !u.IsBanned),
                "已禁用" => _users.Where(u => u.IsBanned),
                _ => _users
            };
            if(filteredUsers == null)
            {
                return;
            }
            UsersGrid.ItemsSource = filteredUsers;
        }

        private void MessageFilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedItem = MessageFilterComboBox.SelectedItem as ComboBoxItem;
            if (selectedItem == null) return;

            var filter = selectedItem.Content.ToString();
            var filteredMessages = filter switch
            {
                "全部" => _messages,
                "文本消息" => _messages.Where(m => m.MessageType == "Text"),
                "文件消息" => _messages.Where(m => m.MessageType == "File"),
                "已删除消息" => _messages.Where(m => m.IsDeleted),
                "未删除消息" => _messages.Where(m => !m.IsDeleted),
                _ => _messages
            };

            if (filteredMessages == null)
            {
                return;
            }
            MessagesGrid.ItemsSource = filteredMessages;
        }

        private void Minimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
