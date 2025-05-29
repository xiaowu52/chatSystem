namespace Web.Client.Config
{
    public static class ApiConfig
    {
        public const string BaseUrl = "https://localhost:5284/api";

        // 用户相关接口
        public static class Users
        {
            public const string Register = $"{BaseUrl}/users/register";
            public const string Login = $"{BaseUrl}/users/login";
            public const string Search = $"{BaseUrl}/users/search";
        }

        // 好友相关接口
        public static class Friends
        {
            public const string List = $"{BaseUrl}/friends/list";
            public const string Request = $"{BaseUrl}/friends/request";
            public const string Respond = $"{BaseUrl}/friends/respond";
            public const string Delete = $"{BaseUrl}/friends/delete";
            public const string RequestsIncoming = $"{BaseUrl}/friends/requests/incoming";
            public const string RequestsOutgoing = $"{BaseUrl}/friends/requests/outgoing";
        }

        // 群组相关接口
        public static class Groups
        {
            public const string Create = $"{BaseUrl}/groups/create";
            public const string List = $"{BaseUrl}/groups/list";
            public const string Members = $"{BaseUrl}/groups/members";
        }

        // 消息相关接口
        public static class Messages
        {
            public const string Send = $"{BaseUrl}/messages/send";
            public const string History = $"{BaseUrl}/messages/history";
        }

        // 文件传输相关接口
        public static class Files
        {
            public const string Upload = $"{BaseUrl}/files/upload";
            public const string Download = $"{BaseUrl}/files/download";
        }
    }
}
