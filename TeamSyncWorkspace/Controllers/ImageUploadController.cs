using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace TeamSyncWorkspace.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ImageUploadController : ControllerBase
    {
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<ImageUploadController> _logger;

        public ImageUploadController(
            IWebHostEnvironment environment,
            ILogger<ImageUploadController> logger)
        {
            _environment = environment;
            _logger = logger;
        }

        [HttpPost]
        public async Task<IActionResult> Upload()
        {
            try
            {
                var file = Request.Form.Files[0];
                if (file == null || file.Length == 0)
                {
                    return BadRequest(new { error = new { message = "No file provided" } });
                }

                // Validate file type
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
                var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();
                if (!Array.Exists(allowedExtensions, ext => ext == fileExtension))
                {
                    return BadRequest(new { error = new { message = "Invalid file type. Only images are allowed." } });
                }

                // Sanitize filename to prevent path traversal
                var fileName = Path.GetFileNameWithoutExtension(file.FileName);
                fileName = Regex.Replace(fileName, @"[^a-zA-Z0-9\-_]", "_");
                fileName = $"{fileName}_{DateTime.Now:yyyyMMddHHmmss}{fileExtension}";

                // Create uploads directory if it doesn't exist
                var uploadPath = Path.Combine(Path.GetTempPath(), "uploads", "ckeditor", "images");
                Directory.CreateDirectory(uploadPath);

                // Save the file
                var filePath = Path.Combine(uploadPath, fileName);
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                // Return the URL to the file
                var url = $"{Request.Scheme}://{Request.Host}/uploads/ckeditor/images/{fileName}";

                return Ok(new
                {
                    uploaded = true,
                    url = url
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading image");
                return StatusCode(500, new { error = new { message = "Error uploading image" } });
            }
        }
    }
}