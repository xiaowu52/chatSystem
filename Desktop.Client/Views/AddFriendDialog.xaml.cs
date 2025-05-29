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
    public partial class AddFriendDialog : Window
    {
        private readonly HttpClient _httpClient;
        private ObservableCollection<UserSearchResult> _searchResults;

        public string UserName { get; private set; }
        public int SelectedUserId { get; private set; }

        public AddFriendDialog(HttpClient httpClient, int currentUserId)
        {
            InitializeComponent();
            _httpClient = httpClient;
            _searchResults = new ObservableCollection<UserSearchResult>();
            SearchResultsList.ItemsSource = _searchResults;
        }

        private async void Search_Click(object sender, RoutedEventArgs e)
        {
            string keyword = UserNameBox.Text.Trim();
            if (string.IsNullOrEmpty(keyword))
            {
                MessageBox.Show("请输入搜索关键词");
                return;
            }

            await SearchUsers(keyword);
        }

        private async Task SearchUsers(string keyword)
        {
            try
            {
                var response = await _httpClient.GetAsync($"https://localhost:5284/api/users/search?keyword={keyword}");

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var users = JsonSerializer.Deserialize<List<UserSearchResult>>(content,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    _searchResults.Clear();
                    foreach (var user in users)
                    {
                        if (user.Id != App.CurrentUser.UserId) // 不显示自己
                        {
                            _searchResults.Add(user);
                        }
                    }

                    if (_searchResults.Count == 0)
                    {
                        MessageBox.Show("未找到匹配的用户");
                    }
                }
                else
                {
                    MessageBox.Show($"搜索失败: {response.ReasonPhrase}");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"搜索失败: {ex.Message}");
            }
        }

        private async void AddFriend_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && int.TryParse(button.Tag.ToString(), out int userId))
            {
                try
                {
                    var request = new
                    {
                        UserId = App.CurrentUser.UserId,
                        FriendId = userId
                    };

                    var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
                    var response = await _httpClient.PostAsync("https://localhost:5284/api/friends/request", content);

                    if (response.IsSuccessStatusCode)
                    {
                        MessageBox.Show("好友请求已发送，等待对方确认", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                        DialogResult = true;
                    }
                    else
                    {
                        var errorContent = await response.Content.ReadAsStringAsync();
                        MessageBox.Show($"请求发送失败: {errorContent}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"请求发送失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }

    public class UserSearchResult
    {
        public int Id { get; set; }
        public string UserName { get; set; }
        public string DisplayName { get; set; }
    }
}
