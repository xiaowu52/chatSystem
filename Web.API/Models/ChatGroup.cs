namespace Web.API.Models
{
    public class ChatGroup
    {
        public int Id { get; set; }
        public string GroupName { get; set; }
        public int OwnerId { get; set; }
        public DateTime CreatedAt { get; set; }

        public User Owner { get; set; }
    }
}
