namespace Web.API.Models
{
    public class Friend
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public int FriendId { get; set; }
        public string Status { get; set; } // Pending, Accepted, Rejected
        public DateTime RequestedAt { get; set; }
        public DateTime? RespondedAt { get; set; }

        public User User { get; set; }
        public User FriendUser { get; set; }
    }
}
