using System.Configuration;
using System.Data;
using System.Windows;

namespace Desktop.Client
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public static SignalRService SignalRService { get; private set; } = new SignalRService();

        // 在登录成功时从 Login.xaml.cs 中调用
        public static async Task InitializeSignalR()
        {
            await SignalRService.InitializeConnection();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            SignalRService?.Disconnect().Wait();
            base.OnExit(e);
        }

        // 创建全局用户信息存储
        private static UserSession _currentUser;

            public static UserSession CurrentUser
            {
                get => _currentUser;
                set
                {
                    _currentUser = value;
                    // 可以在这里添加用户会话变更的事件通知
                }
            }

            // 检查用户是否已登录
            public static bool IsUserLoggedIn => _currentUser != null && !string.IsNullOrEmpty(_currentUser.Token);

        }

        // 用户会话信息类
        public class UserSession
        {
            public int UserId { get; set; }
            public string UserName { get; set; }
            public string DisplayName { get; set; }
            public string Email { get; set; }
            public string Token { get; set; }
            public bool IsApproved { get; set; }
            public bool IsBanned { get; set; }

            // 从登录响应创建会话
            public static UserSession FromLoginResponse(LoginResponse response)
            {
                return new UserSession
                {
                    UserId = response.User.Id,
                    UserName = response.User.UserName,
                    DisplayName = response.User.DisplayName,
                    Email = response.User.Email,
                    Token = response.Token,
                    IsApproved = response.User.IsApproved,
                    IsBanned = response.User.IsBanned
                };
            }
        }

    }
