namespace Miniblog.Core.Controllers
{
    using Microsoft.AspNetCore.Mvc;

    using Miniblog.Core.Database;

    using System.Linq;

    public class FileController : Controller
    {
        private readonly BlogContext blogContext;

        public FileController(BlogContext blogContext)
        {
            this.blogContext = blogContext;
        }

        [Route("/file/{fileName}")]
        public IActionResult GetFile(string fileName)
        {
            var image = this.blogContext.Files.FirstOrDefault(f=>f.FileName == fileName)?.Content;
            if (image == null)
            {
                return NotFound();
            }

            return File(image, "image/png");
        }
    }
}
