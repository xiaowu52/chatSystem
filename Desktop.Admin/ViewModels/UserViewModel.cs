// 创建新文件 Desktop.Admin/ViewModels/UserViewModel.cs
using System.Windows;
using Web.API.Models;

namespace Desktop.Admin.ViewModels
{
    public class UserViewModel
    {
        private readonly User _user;

        public UserViewModel(User user)
        {
            _user = user;
        }

        // 原始User属性的传递
        public int Id => _user.Id;
        public string UserName => _user.UserName;
        public string DisplayName => _user.DisplayName;
        public string Email => _user.Email;
        public DateTime CreatedAt => _user.CreatedAt;
        public bool IsApproved => _user.IsApproved;
        public bool IsBanned => _user.IsBanned;

        // 添加Status属性
        public string Status
        {
            get
            {
                if (_user.IsBanned)
                    return "已禁用";
                if (_user.IsApproved)
                    return "已审核";
                return "待审核";
            }
        }

        // 添加按钮可见性属性
        public Visibility ApproveButtonVisibility
        {
            get
            {
                return (!_user.IsApproved && !_user.IsBanned) ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        public Visibility BanButtonVisibility
        {
            get
            {
                return (_user.IsApproved && !_user.IsBanned) ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        public Visibility UnbanButtonVisibility
        {
            get
            {
                return _user.IsBanned ? Visibility.Visible : Visibility.Collapsed;
            }
        }
    }
}
