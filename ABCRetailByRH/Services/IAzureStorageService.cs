using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using ABCRetailByRH.Models;

namespace ABCRetailByRH.Services
{
    public interface IAzureStorageService
    {
        List<Customer> GetAllCustomers();
        Customer? GetCustomer(string partitionKey, string rowKey);
        void AddCustomer(Customer customer);
        void UpdateCustomer(Customer customer);
        void DeleteCustomer(string partitionKey, string rowKey);

        List<Product> GetAllProducts();
        Product? GetProduct(string partitionKey, string rowKey);
        void AddProduct(Product product, IFormFile? imageFile);
        void UpdateProduct(Product product, IFormFile? newImageFile);
        void DeleteProduct(string partitionKey, string rowKey);

        void EnqueueMessage(string message);
        string? DequeueMessage();
        int GetQueueLength();

        void UploadFile(IFormFile file);
        List<string> ListFiles();
        byte[] DownloadFile(string fileName, out string contentType);
        void DeleteFile(string fileName);
    }
}
