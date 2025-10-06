// ABCRetailByRH/Services/FunctionsClient.cs
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace ABCRetailByRH.Services
{
    public class FunctionsClient : IFunctionsClient
    {
        private readonly HttpClient _http;
        private readonly string _key;
        private readonly string _keyEncoded;

        private static readonly JsonSerializerOptions _json = new()
        {
            PropertyNameCaseInsensitive = true,
            NumberHandling = JsonNumberHandling.AllowReadingFromString
        };

        public FunctionsClient(HttpClient http, IOptions<FunctionsOptions> opts)
        {
            _http = http;
            _key = (opts.Value.Key ?? string.Empty).Trim();
            _keyEncoded = string.IsNullOrWhiteSpace(_key) ? "" : Uri.EscapeDataString(_key);
        }

        private HttpRequestMessage Build(HttpMethod method, string relativePath, HttpContent? content = null)
        {
            // Always include the key in BOTH places:
            // 1) query string (?code=...)  2) x-functions-key header
            string path = relativePath;
            if (!string.IsNullOrEmpty(_keyEncoded) &&
                relativePath.IndexOf("code=", StringComparison.OrdinalIgnoreCase) < 0)
            {
                var sep = relativePath.Contains('?', StringComparison.Ordinal) ? '&' : '?';
                path = $"{relativePath}{sep}code={_keyEncoded}";
            }

            var req = new HttpRequestMessage(method, path); // relative to BaseAddress
            if (!string.IsNullOrEmpty(_key))
                req.Headers.Add("x-functions-key", _key);

            if (content != null) req.Content = content;
            return req;
        }

        // ---------- Customers ----------
        public async Task<FuncCustomerDto> CreateCustomerAsync(string name, string email, string phone, CancellationToken ct = default)
        {
            var body = JsonSerializer.Serialize(new { Name = name, Email = email, Phone = phone }, _json);
            using var req = Build(HttpMethod.Post, "/api/customers",
                new StringContent(body, Encoding.UTF8, "application/json"));
            using var res = await _http.SendAsync(req, ct);
            res.EnsureSuccessStatusCode();
            var json = await res.Content.ReadAsStringAsync(ct);
            return JsonSerializer.Deserialize<FuncCustomerDto>(json, _json)!;
        }

        public async Task<List<FuncCustomerDto>> ListCustomersAsync(CancellationToken ct = default)
        {
            using var req = Build(HttpMethod.Get, "/api/customers");
            using var res = await _http.SendAsync(req, ct);
            res.EnsureSuccessStatusCode();
            var json = await res.Content.ReadAsStringAsync(ct);
            return JsonSerializer.Deserialize<List<FuncCustomerDto>>(json, _json) ?? new();
        }

        public async Task<FuncCustomerDto?> GetCustomerAsync(string partition, string id, CancellationToken ct = default)
        {
            using var req = Build(HttpMethod.Get, $"/api/customers/{Uri.EscapeDataString(partition)}/{Uri.EscapeDataString(id)}");
            using var res = await _http.SendAsync(req, ct);
            if (!res.IsSuccessStatusCode) return null;
            var json = await res.Content.ReadAsStringAsync(ct);
            return JsonSerializer.Deserialize<FuncCustomerDto>(json, _json);
        }

        public async Task<bool> PingAsync(CancellationToken ct = default)
        {
            using var req = Build(HttpMethod.Get, "/api/ping");
            using var res = await _http.SendAsync(req, ct);
            return res.IsSuccessStatusCode;
        }

        // ---------- Orders ----------
        public async Task<bool> EnqueueRawAsync(string rawJson, CancellationToken ct = default)
        {
            var content = new StringContent(rawJson, Encoding.UTF8, "application/json");

            using (var reqNew = Build(HttpMethod.Post, "/api/orders/enqueue", content))
            {
                using var resNew = await _http.SendAsync(reqNew, ct);
                if (resNew.IsSuccessStatusCode) return true;

                if (resNew.StatusCode == HttpStatusCode.NotFound)
                {
                    using var reqOld = Build(HttpMethod.Post, "/api/Orders_Enqueue",
                        new StringContent(rawJson, Encoding.UTF8, "application/json"));
                    using var resOld = await _http.SendAsync(reqOld, ct);
                    return resOld.IsSuccessStatusCode;
                }
                return false;
            }
        }

        public async Task<List<FuncOrderDto>> ListOrdersAsync(int top = 50, string? status = null, CancellationToken ct = default)
        {
            var query = $"?top={top}" + (string.IsNullOrWhiteSpace(status) ? "" : $"&status={Uri.EscapeDataString(status)}");

            using var reqNew = Build(HttpMethod.Get, "/api/orders" + query);
            using var resNew = await _http.SendAsync(reqNew, ct);
            if (resNew.IsSuccessStatusCode)
                return await ParseOrdersAsync(resNew, ct);

            if (resNew.StatusCode == HttpStatusCode.NotFound)
            {
                using var reqOld = Build(HttpMethod.Get, "/api/Orders_List" + query);
                using var resOld = await _http.SendAsync(reqOld, ct);
                resOld.EnsureSuccessStatusCode();
                return await ParseOrdersAsync(resOld, ct);
            }

            resNew.EnsureSuccessStatusCode();
            return new List<FuncOrderDto>();
        }

        private static async Task<List<FuncOrderDto>> ParseOrdersAsync(HttpResponseMessage res, CancellationToken ct)
        {
            var json = await res.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var list = new List<FuncOrderDto>();

            if (root.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array)
            {
                foreach (var e in items.EnumerateArray())
                    list.Add(MapOrder(e));
                return list;
            }

            if (root.ValueKind == JsonValueKind.Array)
            {
                foreach (var e in root.EnumerateArray())
                    list.Add(MapOrder(e));
                return list;
            }

            if (root.ValueKind == JsonValueKind.Object)
                list.Add(MapOrder(root));

            return list;
        }

        private static FuncOrderDto MapOrder(JsonElement e)
        {
            var orderId = FindString(e, "orderId", "orderID", "order_id", "id", "Id", "rowKey", "RowKey", "reference", "ref");
            var customer = FindString(e, "customer", "customerName", "name", "buyer", "client");
            var total = FindDouble(e, "total", "amount", "grandTotal", "value", "price", "sum", "totalAmount", "total_value") ?? 0d;
            var status = FindString(e, "status", "state") ?? "Pending";
            var created = FindDate(e, "createdUtc", "created", "createdAt", "created_at", "timestamp", "ts", "timeCreated");
            var processed = FindDate(e, "processedUtc", "processed", "processedAt", "processed_at", "updatedAt", "completedAt");

            return new FuncOrderDto
            {
                OrderId = orderId ?? string.Empty,
                Customer = customer ?? string.Empty,
                Total = total,
                Status = status,
                CreatedUtc = created,
                ProcessedUtc = processed
            };
        }

        // ---- helpers --------------------------------------------------------
        private static string? FindString(JsonElement obj, params string[] keys)
        {
            foreach (var k in keys)
            {
                if (TryGet(obj, k, out var v))
                {
                    return v.ValueKind switch
                    {
                        JsonValueKind.String => v.GetString(),
                        JsonValueKind.Number => v.ToString(),
                        JsonValueKind.True => "true",
                        JsonValueKind.False => "false",
                        _ => null
                    };
                }
            }
            foreach (var p in obj.EnumerateObject())
            {
                foreach (var k in keys)
                {
                    if (p.Name.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0)
                        return p.Value.ValueKind == JsonValueKind.String ? p.Value.GetString() : p.Value.ToString();
                }
            }
            return null;
        }

        private static double? FindDouble(JsonElement obj, params string[] keys)
        {
            foreach (var k in keys)
            {
                if (TryGet(obj, k, out var v))
                {
                    if (v.ValueKind == JsonValueKind.Number)
                    {
                        if (v.TryGetDouble(out var d)) return d;
                        if (v.TryGetDecimal(out var dec)) return (double)dec;
                    }
                    else if (v.ValueKind == JsonValueKind.String)
                    {
                        var s = v.GetString();
                        if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var d)) return d;
                        if (decimal.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var dec)) return (double)dec;
                    }
                }
            }
            foreach (var p in obj.EnumerateObject())
            {
                if (p.Value.ValueKind == JsonValueKind.Number && p.Value.TryGetDouble(out var d))
                    return d;
                if (p.Value.ValueKind == JsonValueKind.String &&
                    double.TryParse(p.Value.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var ds))
                    return ds;
            }
            return null;
        }

        private static DateTime? FindDate(JsonElement obj, params string[] keys)
        {
            foreach (var k in keys)
            {
                if (TryGet(obj, k, out var v))
                {
                    if (v.ValueKind == JsonValueKind.String)
                    {
                        var s = v.GetString();
                        if (DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture,
                                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dto))
                            return dto.UtcDateTime;

                        if (DateTime.TryParse(s, CultureInfo.InvariantCulture,
                                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dt))
                            return DateTime.SpecifyKind(dt, DateTimeKind.Utc);
                    }
                    else if (v.ValueKind == JsonValueKind.Number && v.TryGetInt64(out var ms))
                    {
                        try { return DateTimeOffset.FromUnixTimeMilliseconds(ms).UtcDateTime; } catch { }
                    }
                }
            }
            return null;
        }

        private static bool TryGet(JsonElement obj, string name, out JsonElement value)
        {
            if (obj.TryGetProperty(name, out value)) return true;

            foreach (var p in obj.EnumerateObject())
            {
                if (string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    value = p.Value;
                    return true;
                }
            }
            value = default;
            return false;
        }
    }
}
