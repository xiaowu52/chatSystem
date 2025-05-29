using System.ComponentModel.DataAnnotations;
namespace Web.API.Models
{
    public class UserRegisterRequest
    {
        [Required(ErrorMessage = "用户名不能为空")]
        public string UserName { get; set; }

        [Required(ErrorMessage = "密码不能为空")]
        public string Password { get; set; }

        [Required(ErrorMessage = "昵称不能为空")]
        public string DisplayName { get; set; }

        [Required(ErrorMessage = "邮箱不能为空")]
        public string Email { get; set; }
    }
}
