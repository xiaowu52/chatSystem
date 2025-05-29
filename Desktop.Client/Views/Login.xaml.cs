using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Net.Http.Headers;
using Microsoft.Win32;
using Desktop.Client.Views;
using Desktop.Client.Config;

namespace Desktop.Client
{
    /// <summary>
    /// Login.xaml 的交互逻辑
    /// </summary>
    public partial class Login : Window
    {
        private readonly HttpClient _httpClient;

        public Login()
        {
            InitializeComponent();
            _httpClient = new HttpClient();
        }

        private async void Login_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var request = new { UserName = UserNameBox.Text, Password = PasswordBox.Password };
                var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync(ApiConfig.Users.Login, content);

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadAsStringAsync();
                    var loginResponse = JsonSerializer.Deserialize<LoginResponse>(
                        result,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                    );

                    // 设置全局用户信息
                    App.CurrentUser = UserSession.FromLoginResponse(loginResponse);

                    // 设置HttpClient的默认Authorization头
                    _httpClient.DefaultRequestHeaders.Authorization =
                        new AuthenticationHeaderValue("Bearer", loginResponse.Token);
                    // 初始化 SignalR 连接
                    await App.InitializeSignalR();


                    MessageBox.Show($"欢迎回来，{App.CurrentUser.DisplayName}！", "登录成功",
                                     MessageBoxButton.OK, MessageBoxImage.Information);

                    // 打开主窗口
                    var mainWindow = new MainWindow(_httpClient);
                    mainWindow.Show();
                    this.Close();
                }
                else
                {
                    MessageBlock.Text = await response.Content.ReadAsStringAsync();
                }
            }
            catch (Exception ex)
            {
                MessageBlock.Text = $"登录失败: {ex.Message}";
            }
        }

        private void Register_Click(object sender, RoutedEventArgs e)
        {
            var registerWindow = new Register();
            registerWindow.Show();
            this.Close();
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

    public class LoginResponse
    {
        public string Token { get; set; }
        public UserResponse User { get; set; }
    }

    public class UserResponse
    {
        public int Id { get; set; }
        public string UserName { get; set; }
        public string DisplayName { get; set; }
        public string Email { get; set; }
        public bool IsApproved { get; set; }
        public bool IsBanned { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
