using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace TeamSyncWorkspace.Controllers
{
    [Route("[controller]")]
    public class FilesController : Controller
    {
        [HttpGet("profile/{filename}")]
        public IActionResult GetProfilePicture(string filename)
        {
            var path = Path.Combine(Path.GetTempPath(), "uploads", "profile", filename);

            if (!System.IO.File.Exists(path))
            {
                return NotFound();
            }

            var fileExtension = Path.GetExtension(filename).ToLowerInvariant();
            string contentType = "image/jpeg"; // Default

            switch (fileExtension)
            {
                case ".png":
                    contentType = "image/png";
                    break;
                case ".gif":
                    contentType = "image/gif";
                    break;
                case ".jpg":
                case ".jpeg":
                    contentType = "image/jpeg";
                    break;
            }

            return PhysicalFile(path, contentType);
        }

        [HttpGet("ckeditor/images/{filename}")]
        public IActionResult GetCKEditorImage(string filename)
        {
            var path = Path.Combine(Path.GetTempPath(), "uploads", "ckeditor", "images", filename);

            if (!System.IO.File.Exists(path))
            {
                return NotFound();
            }

            var fileExtension = Path.GetExtension(filename).ToLowerInvariant();
            string contentType = "image/jpeg"; // Default

            switch (fileExtension)
            {
                case ".png":
                    contentType = "image/png";
                    break;
                case ".gif":
                    contentType = "image/gif";
                    break;
                case ".jpg":
                case ".jpeg":
                    contentType = "image/jpeg";
                    break;
            }

            return PhysicalFile(path, contentType);
        }
    }
}