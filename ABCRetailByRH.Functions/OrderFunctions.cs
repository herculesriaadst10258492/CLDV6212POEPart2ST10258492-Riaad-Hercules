// ABCRetailByRH.Functions/OrderFunctions.cs
using System;
using System.Net;
using System.Text.Json;
using System.Collections.Generic;
using System.Linq;
using Azure.Data.Tables;
using Azure;
using Azure.Storage.Queues;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ABCRetailByRH.Functions
{
    public class OrderFunctions
    {
        private readonly QueueClient _queue;
        private readonly QueueClient _finalizeQueue;
        private readonly TableClient _table;
        private readonly ILogger _log;
        private readonly string _queueName;
        private readonly string _finalizeQueueName;

        private const string OrdersPartition = "ORDER";
        private const string StorageConnectionName = "AzureWebJobsStorage";

        // case-insensitive JSON
        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            PropertyNameCaseInsensitive = true
        };

        // message schema
        public record OrderMsg(string? OrderId = null,
                               string? Customer = null,
                               double? Total = null,
                               DateTime? TimestampUtc = null);

        public OrderFunctions(IConfiguration cfg, ILoggerFactory lf)
        {
            var cs = cfg[StorageConnectionName]
                     ?? throw new InvalidOperationException($"Missing {StorageConnectionName}");

            _queueName = cfg["OrdersQueueName"] ?? "orders";
            _finalizeQueueName = cfg["OrdersFinalizeQueueName"] ?? "orders-finalize";

            _queue = new QueueClient(cs, _queueName, new QueueClientOptions { MessageEncoding = QueueMessageEncoding.Base64 });
            _finalizeQueue = new QueueClient(cs, _finalizeQueueName, new QueueClientOptions { MessageEncoding = QueueMessageEncoding.Base64 });

            _queue.CreateIfNotExists();
            _finalizeQueue.CreateIfNotExists();

            var tableName = cfg["OrdersTableName"] ?? "Orders";
            _table = new TableClient(cs, tableName);
            _table.CreateIfNotExists();

            _log = lf.CreateLogger<OrderFunctions>();
            _log.LogInformation("OrderFunctions constructed. Queue={Queue}, FinalizeQueue={Finalize}, Table={Table}",
                _queueName, _finalizeQueueName, tableName);
        }

        // -------------------------
        // 1) HTTP -> enqueue order
        //    Route: /api/orders/enqueue
        // -------------------------
        [Function("Orders_Enqueue")]
        public async Task<HttpResponseData> Enqueue(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "orders/enqueue")] HttpRequestData req)
        {
            var incoming = await JsonSerializer.DeserializeAsync<OrderMsg>(req.Body, JsonOpts) ?? new();

            var order = new OrderMsg(
                incoming.OrderId ?? Guid.NewGuid().ToString("N"),
                string.IsNullOrWhiteSpace(incoming.Customer) ? "Anonymous" : incoming.Customer,
                incoming.Total ?? 0,
                incoming.TimestampUtc ?? DateTime.UtcNow
            );

            var message = JsonSerializer.Serialize(order);
            await _queue.SendMessageAsync(message);

            var res = req.CreateResponse(HttpStatusCode.Accepted);
            await res.WriteAsJsonAsync(new { queued = true, queue = _queueName, orderId = order.OrderId });
            _log.LogInformation("Enqueued order {OrderId} for {Customer}", order.OrderId, order.Customer);
            return res;
        }

        // -------------------------------------------------
        // 2) QueueTrigger (orders) -> seed "Pending" row
        //    then hand off to orders-finalize
        // -------------------------------------------------
        [Function("Orders_Process")]
        public async Task ProcessAsync(
            [QueueTrigger("%OrdersQueueName%", Connection = StorageConnectionName)] string message)
        {
            _log.LogInformation("Orders_Process received message: {Snippet}", message?.Length > 128 ? message[..128] + "..." : message);

            OrderMsg data;
            try
            {
                data = JsonSerializer.Deserialize<OrderMsg>(message, JsonOpts) ?? new();
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Failed to deserialize queue message.");
                throw; // move to poison after retries
            }

            var orderId = string.IsNullOrWhiteSpace(data.OrderId) ? Guid.NewGuid().ToString("N") : data.OrderId!;
            var createdUtc = DateTime.UtcNow;

            var entity = new TableEntity(OrdersPartition, orderId)
            {
                ["Customer"] = data.Customer ?? "Unknown",
                ["Total"] = data.Total ?? 0,
                ["Status"] = "Pending",
                ["CreatedUtc"] = createdUtc
            };

            await _table.UpsertEntityAsync(entity, TableUpdateMode.Replace);
            _log.LogInformation("Seeded PENDING order {OrderId} for {Customer}", orderId, data.Customer);

            await _finalizeQueue.SendMessageAsync(JsonSerializer.Serialize(data with { OrderId = orderId }, JsonOpts));
            _log.LogInformation("Forwarded order {OrderId} to finalize queue {FinalizeQueue}", orderId, _finalizeQueueName);
        }

        // -------------------------------------------------
        // 3) QueueTrigger (orders-finalize) -> set Processed
        // -------------------------------------------------
        [Function("Orders_Finalize")]
        public async Task FinalizeAsync(
            [QueueTrigger("%OrdersFinalizeQueueName%", Connection = StorageConnectionName)] string message)
        {
            _log.LogInformation("Orders_Finalize received message.");

            var data = JsonSerializer.Deserialize<OrderMsg>(message, JsonOpts) ?? new();
            if (string.IsNullOrWhiteSpace(data.OrderId))
            {
                _log.LogWarning("Finalize: message missing OrderId. Skipping.");
                return;
            }

            var response = await _table.GetEntityIfExistsAsync<TableEntity>(OrdersPartition, data.OrderId);
            if (!response.HasValue)
            {
                _log.LogWarning("Finalize: order {OrderId} not found.", data.OrderId);
                return;
            }

            var e = response.Value;
            e["Status"] = "Processed";
            e["ProcessedUtc"] = DateTime.UtcNow;

            await _table.UpdateEntityAsync(e, e.ETag, TableUpdateMode.Merge);
            _log.LogInformation("Order {OrderId} finalized to PROCESSED.", data.OrderId);
        }

        // -------------------------------------------------
        // 4) HTTP -> list orders
        //    Route: /api/orders
        //    Supports ?top=50 and optional ?status=Pending/Processed
        // -------------------------------------------------
        [Function("Orders_List")]
        public async Task<HttpResponseData> ListAsync(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "orders")] HttpRequestData req)
        {
            var qp = ParseQuery(req.Url.Query);
            var top = qp.TryGetValue("top", out var topRaw) && int.TryParse(topRaw, out var t) ? t : 25;
            qp.TryGetValue("status", out var statusFilter);

            string? filter = $"PartitionKey eq '{OrdersPartition}'";
            if (!string.IsNullOrWhiteSpace(statusFilter))
            {
                filter += $" and Status eq '{statusFilter}'";
            }

            var items = new List<object>();
            await foreach (var e in _table.QueryAsync<TableEntity>(filter))
            {
                e.TryGetValue("Total", out var totalObj);
                e.TryGetValue("Customer", out var customerObj);
                e.TryGetValue("Status", out var statusObj);
                e.TryGetValue("CreatedUtc", out var createdObj);
                e.TryGetValue("ProcessedUtc", out var processedObj);

                items.Add(new
                {
                    OrderId = e.RowKey,
                    Customer = customerObj?.ToString() ?? "Unknown",
                    Total = totalObj is null ? 0 : Convert.ToDouble(totalObj),
                    Status = statusObj?.ToString() ?? "",
                    CreatedUtc = createdObj as DateTime? ?? (createdObj is DateTimeOffset cdo ? cdo.UtcDateTime : null),
                    ProcessedUtc = processedObj as DateTime? ?? (processedObj is DateTimeOffset pdo ? pdo.UtcDateTime : null)
                });
            }

            var ordered = items
                .OrderByDescending(x => (x as dynamic).CreatedUtc ?? DateTime.MinValue)
                .Take(top)
                .ToList();

            var res = req.CreateResponse(HttpStatusCode.OK);
            await res.WriteAsJsonAsync(new { count = ordered.Count, items = ordered });
            return res;
        }

        private static Dictionary<string, string> ParseQuery(string query)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(query)) return result;
            if (query.StartsWith("?")) query = query[1..];

            foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var kv = pair.Split('=', 2);
                var key = Uri.UnescapeDataString(kv[0]);
                var val = kv.Length > 1 ? Uri.UnescapeDataString(kv[1]) : "";
                result[key] = val;
            }
            return result;
        }
    }
}
