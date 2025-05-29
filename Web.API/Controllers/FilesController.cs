using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Web.API.Models;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Text;

namespace Web.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class FilesController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _env;
        public FilesController(AppDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        // 上传文件
        [HttpPost("upload")]
        public async Task<IActionResult> Upload([FromForm] FilesUploadRequest request)
        {
            if (request.File == null || request.File.Length == 0)
                return BadRequest("未选择文件");

            var fileName = Guid.NewGuid() + Path.GetExtension(request.File.FileName);
            var filePath = Path.Combine(_env.ContentRootPath, "Uploads", fileName);

            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await request.File.CopyToAsync(stream);
            }

            var dbFile = new Files
            {
                FileName = request.File.FileName,
                FilePath = fileName,
                UploadedBy = request.UploadedBy,
                UploadedAt = DateTime.UtcNow
            };
            _context.Files.Add(dbFile);
            await _context.SaveChangesAsync();

            return Ok(new { dbFile.Id, dbFile.FileName });
        }

        // 下载文件
        [HttpGet("{id}/download")]
        [Authorize] // 使用标准的授权特性
        public async Task<IActionResult> Download(int id)
        {
            try
            {
                var dbFile = await _context.Files.FindAsync(id);
                if (dbFile == null) return NotFound(new { message = "文件不存在" });

                var filePath = Path.Combine(_env.ContentRootPath, "Uploads", dbFile.FilePath);
                if (!System.IO.File.Exists(filePath)) return NotFound(new { message = "文件不存在" });

                // 记录下载请求
                Console.WriteLine($"文件下载请求: ID={id}, 文件名={dbFile.FileName}, 用户={User?.Identity?.Name ?? "未知用户"}");

                var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
                return File(stream, "application/octet-stream", dbFile.FileName);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"文件下载错误: {ex.Message}");
                return StatusCode(500, new { message = "文件下载失败", error = ex.Message });
            }
        }
    }
}
