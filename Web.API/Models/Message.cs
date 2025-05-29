namespace Web.API.Models
{
    public class Message
    {
        public int Id { get; set; }
        public int SenderId { get; set; }
        public int? ReceiverId { get; set; }
        public int? GroupId { get; set; }
        public string Content { get; set; }
        public string MessageType { get; set; } // Text, File
        public int? FileId { get; set; }
        public DateTime SentAt { get; set; }
        public bool IsDeleted { get; set; }

        public User Sender { get; set; }
        public User Receiver { get; set; }
        public ChatGroup Group { get; set; }
        public Files File { get; set; }
    }
}
