using Microsoft.AspNetCore.Mvc;
using MunicipalReportsAPI.Data;

namespace MunicipalReportsAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ImagesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;

        public ImagesController(ApplicationDbContext context, IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }

        [HttpPost("upload/{reportId}")]
        public async Task<IActionResult> UploadImage(int reportId, IFormFile file)
        {
            var report = await _context.Reports.FindAsync(reportId);
            if (report == null)
            {
                return NotFound(new { success = false, message = "Report not found" });
            }

            if (file == null || file.Length == 0)
            {
                return BadRequest(new { success = false, message = "No file provided" });
            }

            // Validate file type
            var allowedTypes = new[] { "image/jpeg", "image/png", "image/jpg" };
            if (!allowedTypes.Contains(file.ContentType.ToLower()))
            {
                return BadRequest(new { success = false, message = "Only JPEG and PNG files are allowed" });
            }

            // Validate file size (max 5MB)
            if (file.Length > 5 * 1024 * 1024)
            {
                return BadRequest(new { success = false, message = "File size must be less than 5MB" });
            }

            var uploadsPath = Path.Combine(_environment.WebRootPath, "uploads");
            if (!Directory.Exists(uploadsPath))
            {
                Directory.CreateDirectory(uploadsPath);
            }

            // Delete old image if exists
            if (!string.IsNullOrEmpty(report.ReportImage))
            {
                var oldImagePath = Path.Combine(_environment.WebRootPath, report.ReportImage.TrimStart('/'));
                if (System.IO.File.Exists(oldImagePath))
                {
                    System.IO.File.Delete(oldImagePath);
                }
            }

            // Save new image
            var fileName = $"{reportId}_{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
            var filePath = Path.Combine(uploadsPath, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            // Update report with image path
            report.ReportImage = $"/uploads/{fileName}";
            report.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                message = "Image uploaded successfully",
                imagePath = report.ReportImage
            });
        }

        [HttpGet("view/{filename}")]
        public IActionResult ViewImage(string filename)
        {
            var filePath = Path.Combine(_environment.WebRootPath, "uploads", filename);

            if (!System.IO.File.Exists(filePath))
            {
                return NotFound();
            }

            var contentType = GetContentType(filename);
            return PhysicalFile(filePath, contentType);
        }

        [HttpDelete("{reportId}")]
        public async Task<IActionResult> DeleteImage(int reportId)
        {
            var report = await _context.Reports.FindAsync(reportId);
            if (report == null)
            {
                return NotFound(new { success = false, message = "Report not found" });
            }

            if (string.IsNullOrEmpty(report.ReportImage))
            {
                return BadRequest(new { success = false, message = "No image to delete" });
            }

            // Delete physical file
            var imagePath = Path.Combine(_environment.WebRootPath, report.ReportImage.TrimStart('/'));
            if (System.IO.File.Exists(imagePath))
            {
                System.IO.File.Delete(imagePath);
            }

            // Remove from database
            report.ReportImage = string.Empty;
            report.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Image deleted successfully" });
        }

        private string GetContentType(string filename)
        {
            var extension = Path.GetExtension(filename).ToLowerInvariant();
            return extension switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                _ => "application/octet-stream"
            };
        }
    }
}