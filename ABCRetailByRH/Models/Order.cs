using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Azure;
using Azure.Data.Tables;
using Microsoft.AspNetCore.Http;

namespace ABCRetailByRH.Models
{
    /// <summary>
    /// Order entity stored in Azure Table Storage but also shaped
    /// to match the existing Razor views (Id, OrderId, CustomerId, etc.).
    /// </summary>
    public class Order : ITableEntity
    {
        // ===== Azure Table mandatory members =====
        public string PartitionKey { get; set; } = "ORDERS";
        public string RowKey { get; set; } = Guid.NewGuid().ToString("N");
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }

        // ===== View-friendly aliases (the views bind to these) =====
        // Many of your .cshtml files use Id and/or OrderId; map both to RowKey.
        [NotMapped]
        public string Id
        {
            get => RowKey;
            set => RowKey = string.IsNullOrWhiteSpace(value) ? RowKey : value;
        }

        [Display(Name = "Order ID")]
        [NotMapped]
        public string OrderId
        {
            get => RowKey;
            set => RowKey = string.IsNullOrWhiteSpace(value) ? RowKey : value;
        }

        // Foreign keys used by the views
        [Required, Display(Name = "Customer")]
        public string CustomerId { get; set; } = string.Empty;

        [Required, Display(Name = "Product")]
        public string ProductId { get; set; } = string.Empty;

        // These help when you list readable names (some views use them)
        public string? CustomerName { get; set; }
        public string? ProductName { get; set; }

        // Other fields referenced by the views
        [Required]
        public string Username { get; set; } = string.Empty;

        [Range(1, int.MaxValue, ErrorMessage = "Quantity must be at least 1")]
        public int Quantity { get; set; } = 1;

        // Pricing (safe default range for decimal)
        [Range(typeof(decimal), "0", "79228162514264337593543950335")]
        public decimal UnitPrice { get; set; }

        // Some of your views referenced TotalPrice; expose it as a bindable computed field.
        [DataType(DataType.Currency)]
        public decimal TotalPrice
        {
            get => UnitPrice * Quantity;
            set { /* kept to satisfy model binding if a form posts TotalPrice */ }
        }

        // Contract/file metadata referenced in Details/Edit views
        public string? ContractFileName { get; set; }
        public string? ContractOriginalFileName { get; set; }
        public string? ContractContentType { get; set; }

        // Optional: posted file when creating/updating (not stored in Table directly)
        [NotMapped]
        public IFormFile? ContractFile { get; set; }

        // Useful audit info
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public string Status { get; set; } = "Pending";
    }
}
