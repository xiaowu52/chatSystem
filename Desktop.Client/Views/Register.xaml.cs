using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Net.Http;
using System.Text.Json;
using Desktop.Client.Views;
using Desktop.Client.Config;

namespace Desktop.Client
{
    /// <summary>
    /// Register.xaml 的交互逻辑
    /// </summary>
    public partial class Register : Window
    {
        private readonly HttpClient _httpClient;
        private const int MaxRetries = 3;
        private const int RetryDelayMs = 1000;

        public Register()
        {
            InitializeComponent();
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(30) // 设置超时时间为30秒
            };
        }

        private async void Register_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(UserNameBox.Text) || 
                string.IsNullOrEmpty(PasswordBox.Password) ||
                string.IsNullOrEmpty(DisplayNameBox.Text) ||
                string.IsNullOrEmpty(EmailBox.Text))
            {
                MessageBox.Show("所有字段都必须填写");
                return;
            }

            var user = new
            {
                UserName = UserNameBox.Text,
                Password = PasswordBox.Password,
                DisplayName = DisplayNameBox.Text,
                Email = EmailBox.Text
            };

            var content = new StringContent(
                JsonSerializer.Serialize(user),
                Encoding.UTF8,
                "application/json");

            for (int i = 0; i < MaxRetries; i++)
            {
                try
                {
                    var response = await _httpClient.PostAsync(ApiConfig.Users.Register, content);
                    var result = await response.Content.ReadAsStringAsync();
                    
                    if (response.IsSuccessStatusCode)
                    {
                        var registerResponse = JsonSerializer.Deserialize<RegisterResponse>(result);
                        MessageBox.Show(registerResponse?.Message ?? "注册成功，请等待管理员审核");
                        var loginWindow = new Login();
                        loginWindow.Show();
                        this.Close();
                        return;
                    }
                    else
                    {
                        // 尝试解析错误响应
                        try
                        {
                            var errorResponse = JsonSerializer.Deserialize<ErrorResponse>(result);
                            MessageBox.Show($"注册失败: {errorResponse?.Message ?? result}");
                        }
                        catch
                        {
                            MessageBox.Show($"注册失败: {result}");
                        }
                        return;
                    }
                }
                catch (HttpRequestException ex)
                {
                    if (i == MaxRetries - 1) // 最后一次重试
                    {
                        MessageBox.Show($"注册失败: {ex.Message}\n请检查网络连接或稍后重试。");
                        return;
                    }
                    await Task.Delay(RetryDelayMs * (i + 1)); // 递增延迟
                }
                catch (TaskCanceledException)
                {
                    if (i == MaxRetries - 1)
                    {
                        MessageBox.Show("请求超时，请检查网络连接或稍后重试。");
                        return;
                    }
                    await Task.Delay(RetryDelayMs * (i + 1));
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"发生错误: {ex.Message}");
                    return;
                }
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void BackToLogin_Click(object sender, RoutedEventArgs e)
        {
            Login loginWindow = new Login();
            loginWindow.Show();
            this.Close();
        }
    }

    public class RegisterResponse
    {
        public string Message { get; set; }
    }

    public class ErrorResponse
    {
        public string Message { get; set; }
    }
}
