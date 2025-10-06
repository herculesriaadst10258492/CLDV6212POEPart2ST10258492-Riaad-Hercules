using System;
using Azure;
using Azure.Data.Tables;
using System.ComponentModel.DataAnnotations;

namespace ABCRetailByRH.Models
{
    public class Product : ITableEntity
    {
        // Azure Table key properties
        public string PartitionKey { get; set; } = string.Empty;
        public string RowKey { get; set; } = string.Empty;
        public DateTimeOffset? Timestamp { get; set; } = null;
        public ETag ETag { get; set; }

        // Product properties
        [Required(ErrorMessage = "Product name is required")]
        public string Name { get; set; } = string.Empty;

        // Use double (Azure Tables won’t persist decimal)
        [Required(ErrorMessage = "Price is required")]
        [Range(0, 1_000_000, ErrorMessage = "Price must be between 0 and 1000000")]
        public double Price { get; set; }

        [Required(ErrorMessage = "Stock is required")]
        [Range(0, 100, ErrorMessage = "Stock must be between 0 and 100")]
        public int Stock { get; set; }

        // URL of the product image in Blob storage (if any)
        public string? ImageUrl { get; set; }
    }
}
