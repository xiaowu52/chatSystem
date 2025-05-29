namespace Web.API.Models
{
    public class GroupMember
    {
        public int Id { get; set; }
        public int GroupId { get; set; }
        public int UserId { get; set; }
        public DateTime JoinedAt { get; set; }

        public ChatGroup Group { get; set; }
        public User User { get; set; }
    }
}
