namespace Desktop.Client.Config
{
    public static class ApiConfig
    {
        public const string BaseUrl = "https://localhost:5284/api";
        
        // 用户相关接口
        public static class Users
        {
            public const string Register = $"{BaseUrl}/users/register";
            public const string Login = $"{BaseUrl}/users/login";
            public const string AddFriend = $"{BaseUrl}/users/addfriend";
            public const string DeleteFriend = $"{BaseUrl}/users/deletefriend";
            public const string GetFriends = $"{BaseUrl}/users/friends";
        }

        // 群组相关接口
        public static class Groups
        {
            public const string Create = $"{BaseUrl}/groups/create";
            public const string Join = $"{BaseUrl}/groups/join";
            public const string Leave = $"{BaseUrl}/groups/leave";
            public const string GetGroups = $"{BaseUrl}/groups/list";
        }

        // 消息相关接口
        public static class Messages
        {
            public const string Send = $"{BaseUrl}/messages/send";
            public const string GetHistory = $"{BaseUrl}/messages/history";
            public const string Delete = $"{BaseUrl}/messages/delete";
        }

        // 文件传输相关接口
        public static class Files
        {
            public const string Upload = $"{BaseUrl}/files/upload";
            public const string Download = $"{BaseUrl}/files/download";
        }
    }
} 