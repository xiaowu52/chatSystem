namespace Web.API.Models
{
    public class Files
    {
        public int Id { get; set; }
        public string FileName { get; set; }
        public string FilePath { get; set; }
        public int UploadedBy { get; set; }
        public DateTime UploadedAt { get; set; }

        public User Uploader { get; set; }
    }
}
