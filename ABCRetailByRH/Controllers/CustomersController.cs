using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using ABCRetailByRH.Models;
using ABCRetailByRH.Services;

namespace ABCRetailByRH.Controllers
{
    public class CustomersController : Controller
    {
        private readonly IFunctionsClient _fx;
        private readonly IAzureStorageService _store;

        public CustomersController(IFunctionsClient fx, IAzureStorageService store)
        {
            _fx = fx;
            _store = store;
        }

        public async Task<IActionResult> Index(CancellationToken ct)
        {
            var list = await _fx.ListCustomersAsync(ct);
            var vm = list.Select(Map).ToList();
            return View(vm);
        }

        public async Task<IActionResult> Details(string partition, string id, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(partition) || string.IsNullOrWhiteSpace(id)) return NotFound();
            var dto = await _fx.GetCustomerAsync(partition, id, ct);
            if (dto == null) return NotFound();
            return View(Map(dto));
        }

        [HttpGet]
        public IActionResult Create() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Name,Email,Phone")] Customer customer, CancellationToken ct)
        {
            if (!ModelState.IsValid) return View(customer);
            var created = await _fx.CreateCustomerAsync(customer.Name, customer.Email, customer.Phone, ct);
            if (created == null)
            {
                ModelState.AddModelError("", "The Functions API didn’t return a result.");
                return View(customer);
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Edit(string partition, string id, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(partition) || string.IsNullOrWhiteSpace(id)) return NotFound();
            var dto = await _fx.GetCustomerAsync(partition, id, ct);
            if (dto == null) return NotFound();
            return View(Map(dto));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit([Bind("PartitionKey,RowKey,Name,Email,Phone")] Customer customer)
        {
            if (!ModelState.IsValid) return View(customer);
            _store.UpdateCustomer(customer);
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Delete(string partition, string id, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(partition) || string.IsNullOrWhiteSpace(id)) return NotFound();
            var dto = await _fx.GetCustomerAsync(partition, id, ct);
            if (dto == null) return NotFound();
            return View(Map(dto));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Delete(string partitionKey, string rowKey)
        {
            if (string.IsNullOrWhiteSpace(partitionKey) || string.IsNullOrWhiteSpace(rowKey)) return NotFound();
            _store.DeleteCustomer(partitionKey, rowKey);
            return RedirectToAction(nameof(Index));
        }

        private static Customer Map(FuncCustomerDto d) => new Customer
        {
            PartitionKey = d.partition,
            RowKey = d.id,
            Name = d.name,
            Email = d.email,
            Phone = d.phone
        };
    }
}
