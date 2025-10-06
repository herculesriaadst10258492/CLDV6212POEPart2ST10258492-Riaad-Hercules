// ABCRetailByRH/Controllers/OrdersController.cs
using Microsoft.AspNetCore.Mvc;
using ABCRetailByRH.Services;
using ABCRetailByRH.ViewModels;
using System.Linq;
using System.Text.Json;

namespace ABCRetailByRH.Controllers
{
    public class OrdersController : Controller
    {
        private readonly IFunctionsClient _fx;
        private readonly IAzureStorageService _storageService; // kept in case you use elsewhere

        public OrdersController(IFunctionsClient fx, IAzureStorageService storageService)
        {
            _fx = fx;
            _storageService = storageService;
        }

        // GET: Orders -> status tracker table
        public async Task<IActionResult> Index()
        {
            var dtos = await _fx.ListOrdersAsync(top: 100);
            var vms = dtos.Select(d => new OrderVm
            {
                OrderId = d.OrderId,
                Customer = d.Customer,
                Total = d.Total,
                Status = d.Status,
                CreatedUtc = d.CreatedUtc,
                ProcessedUtc = d.ProcessedUtc
            }).ToList();

            return View(vms);
        }

        // GET: Orders/Create -> nice form
        [HttpGet]
        public IActionResult Create() => View(new CreateOrderInput());

        // POST: Orders/Create -> enqueues via Functions HTTP
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateOrderInput input)
        {
            if (!ModelState.IsValid) return View(input);

            var payload = JsonSerializer.Serialize(new { OrderId = (string?)null, Customer = input.Customer, Total = input.Total });
            var ok = await _fx.EnqueueRawAsync(payload);

            TempData["Msg"] = ok
                ? "Order enqueued. Watch the status flip from Pending to Processed in a few seconds."
                : "Failed to enqueue order.";
            return RedirectToAction(nameof(Index));
        }

        public class CreateOrderInput
        {
            [System.ComponentModel.DataAnnotations.Required]
            public string Customer { get; set; } = "";
            [System.ComponentModel.DataAnnotations.Range(0.01, double.MaxValue)]
            public double Total { get; set; }
        }
    }
}
