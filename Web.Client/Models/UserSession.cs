namespace Web.Client.Models
{
    public class UserSession
    {
        public int UserId { get; set; }
        public string UserName { get; set; }
        public string DisplayName { get; set; }
        public string Email { get; set; }
        public string Token { get; set; }
        public bool IsApproved { get; set; }
        public bool IsBanned { get; set; }

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
