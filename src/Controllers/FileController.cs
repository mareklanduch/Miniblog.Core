namespace Miniblog.Core.Controllers
{
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.EntityFrameworkCore;

    using Miniblog.Core.Database;

    using System.IO;
    using System.Threading.Tasks;

    public class FileController : Controller
    {
        private readonly BlogContext blogContext;

        public FileController(BlogContext blogContext)
        {
            this.blogContext = blogContext;
        }

        [Route("/file/{fileName}")]
        public async Task<IActionResult> GetFile(string fileName)
        {
            var file = await blogContext.Files
                .AsNoTracking()
                .FirstOrDefaultAsync(f => f.FileName == fileName);

            if (file is null)
                return NotFound();

            var contentType = Path.GetExtension(fileName).ToLowerInvariant() switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".gif"            => "image/gif",
                ".webp"           => "image/webp",
                ".svg"            => "image/svg+xml",
                _                 => "image/png"
            };

            return File(file.Content, contentType);
        }
    }
}
