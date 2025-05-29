namespace Web.API.Models
{
    public class FilesUploadRequest
    {
        public IFormFile File { get; set; }
        public int UploadedBy { get; set; }
    }
}
