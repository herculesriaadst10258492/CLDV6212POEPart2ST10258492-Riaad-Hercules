// ABCRetailByRH/Services/IFunctionsClient.cs
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ABCRetailByRH.Services
{
    // Matches your Functions’ customer payloads
    public record FuncCustomerDto
    {
        public string id { get; init; } = "";
        public string partition { get; init; } = "";
        public string name { get; init; } = "";
        public string email { get; init; } = "";
        public string phone { get; init; } = "";
    }

    // NEW: Orders DTO returned by /api/orders from OrderFunctions.Orders_List
    public record FuncOrderDto
    {
        public string OrderId { get; init; } = "";
        public string Customer { get; init; } = "";
        public double Total { get; init; }
        public string Status { get; init; } = "";
        public System.DateTime? CreatedUtc { get; init; }
        public System.DateTime? ProcessedUtc { get; init; }
    }

    public interface IFunctionsClient
    {
        // Customers
        Task<FuncCustomerDto> CreateCustomerAsync(string name, string email, string phone, CancellationToken ct = default);
        Task<List<FuncCustomerDto>> ListCustomersAsync(CancellationToken ct = default);
        Task<FuncCustomerDto?> GetCustomerAsync(string partition, string id, CancellationToken ct = default);
        Task<bool> PingAsync(CancellationToken ct = default);

        // Orders
        // rawJson example: {"OrderId":"A3001","Customer":"cust-001","Total":99.99}
        Task<bool> EnqueueRawAsync(string rawJson, CancellationToken ct = default);

        // NEW: List orders for the status tracker UI
        Task<List<FuncOrderDto>> ListOrdersAsync(int top = 50, string? status = null, CancellationToken ct = default);
    }
}
