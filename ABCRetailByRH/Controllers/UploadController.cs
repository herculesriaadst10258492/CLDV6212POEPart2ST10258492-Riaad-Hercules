using Microsoft.AspNetCore.Mvc;
using ABCRetailByRH.Services;

namespace ABCRetailByRH.Controllers
{
    public class UploadController : Controller
    {
        private readonly IAzureStorageService _svc;

        public UploadController(IAzureStorageService svc)
        {
            _svc = svc;
        }

        [HttpGet]
        public IActionResult Index()
        {
            var files = _svc.ListFiles();
            ViewBag.Message = TempData["Message"];
            return View(files);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Index(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                TempData["Message"] = "Choose a file.";
                return RedirectToAction(nameof(Index));
            }

            _svc.UploadFile(file);
            TempData["Message"] = "Uploaded.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public IActionResult Download(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return BadRequest();

            var bytes = _svc.DownloadFile(name, out string contentType);
            if (bytes == null || bytes.Length == 0)
                return NotFound();

            return File(bytes, string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType, name);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Delete(string name)
        {
            if (!string.IsNullOrWhiteSpace(name))
            {
                _svc.DeleteFile(name);
                TempData["Message"] = "Deleted.";
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public IActionResult Details(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return NotFound();
            ViewBag.Name = name;
            return View(model: $"Details not implemented for '{name}' yet.");
        }
    }
}
