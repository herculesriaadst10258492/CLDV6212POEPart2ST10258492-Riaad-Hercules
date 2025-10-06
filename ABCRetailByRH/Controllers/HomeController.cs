using ABCRetailByRH.Models;
using ABCRetailByRH.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Linq;

namespace ABCRetailByRH.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly IAzureStorageService _svc;

        public HomeController(ILogger<HomeController> logger, IAzureStorageService svc)
        {
            _logger = logger;
            _svc = svc;
        }

        public IActionResult Index()
        {
            // Pick up to 5 products to feature (latest first if possible)
            var all = _svc.GetAllProducts();
            var featured = all
                .OrderByDescending(p => p.Timestamp ?? System.DateTimeOffset.MinValue)
                .Take(5)
                .ToList();

            return View(new HomeViewModel { FeaturedProducts = featured });
        }

        public IActionResult Privacy() => View();

        // --- Contact (GET only, since you embed Google Forms) ---
        [HttpGet]
        public IActionResult Contact() => View();

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
            => View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
