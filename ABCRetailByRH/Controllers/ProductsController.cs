using ABCRetailByRH.Models;
using ABCRetailByRH.Services;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;

namespace ABCRetailByRH.Controllers
{
    public class ProductsController : Controller
    {
        private readonly IAzureStorageService _svc;
        private const string DefaultPK = "PRODUCTS";

        public ProductsController(IAzureStorageService svc) => _svc = svc;

        public IActionResult Index()
        {
            var items = _svc.GetAllProducts();
            return View(items);
        }

        private Product? FindByRowKey(string id)
            => _svc.GetAllProducts().FirstOrDefault(p => p.RowKey == id);

        public IActionResult Details(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return NotFound();

            var item = _svc.GetProduct(DefaultPK, id) ?? FindByRowKey(id);
            if (item == null) return NotFound();

            return View(item);
        }

        public IActionResult Create() => View();

        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult Create(Product model, IFormFile? imageFile)
        {
            if (!ModelState.IsValid) return View(model);

            model.PartitionKey = string.IsNullOrWhiteSpace(model.PartitionKey) ? DefaultPK : model.PartitionKey;
            model.RowKey = string.IsNullOrWhiteSpace(model.RowKey) ? Guid.NewGuid().ToString("N") : model.RowKey;

            _svc.AddProduct(model, imageFile);
            return RedirectToAction(nameof(Index));
        }

        public IActionResult Edit(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return NotFound();

            var item = _svc.GetProduct(DefaultPK, id) ?? FindByRowKey(id);
            if (item == null) return NotFound();

            return View(item);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult Edit(string id, Product model, IFormFile? newImageFile)
        {
            if (!ModelState.IsValid) return View(model);

            // preserve original PK if item was seeded
            var existing = _svc.GetProduct(DefaultPK, id) ?? FindByRowKey(id);
            model.PartitionKey = existing?.PartitionKey ?? DefaultPK;
            model.RowKey = id;

            _svc.UpdateProduct(model, newImageFile);
            return RedirectToAction(nameof(Index));
        }

        public IActionResult Delete(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return NotFound();

            var item = _svc.GetProduct(DefaultPK, id) ?? FindByRowKey(id);
            if (item == null) return NotFound();

            return View(item);
        }

        [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
        public IActionResult DeleteConfirmed(string id)
        {
            var existing = _svc.GetProduct(DefaultPK, id) ?? FindByRowKey(id);
            if (existing == null) return NotFound();

            _svc.DeleteProduct(existing.PartitionKey, existing.RowKey);
            return RedirectToAction(nameof(Index));
        }
    }
}
