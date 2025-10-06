using System.Net;
using System.Text.Json;
using Azure;
using Azure.Data.Tables;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace ABCRetailByRH.Functions;

public class CustomersFunctions
{
    private readonly TableServiceClient _tables;
    private const string TableName = "Customers";

    public CustomersFunctions(StorageClients storage)
    {
        _tables = storage.Tables;
    }

    private TableClient GetTable()
    {
        var table = _tables.GetTableClient(TableName);
        table.CreateIfNotExists();
        return table;
    }

   
    private record CreateCustomerDto(string Name, string Email, string? Phone);

  
    [Function("Customers_List")]
    public async Task<HttpResponseData> List(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "customers")] HttpRequestData req)
    {
        var table = GetTable();

        var list = new List<object>();
        await foreach (var e in table.QueryAsync<TableEntity>())
        {
            list.Add(new
            {
                id = e.RowKey,
                partition = e.PartitionKey,
                name = e.GetString("Name"),
                email = e.GetString("Email"),
                phone = e.GetString("Phone")
            });
        }

        var res = req.CreateResponse(HttpStatusCode.OK);
        await res.WriteAsJsonAsync(list);
        return res;
    }

    
    [Function("Customers_Create")]
    public async Task<HttpResponseData> Create(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "customers")] HttpRequestData req)
    {
        var dto = await JsonSerializer.DeserializeAsync<CreateCustomerDto>(
            req.Body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (dto is null || string.IsNullOrWhiteSpace(dto.Name) || string.IsNullOrWhiteSpace(dto.Email))
        {
            var bad = req.CreateResponse(HttpStatusCode.BadRequest);
            await bad.WriteStringAsync("Name and Email are required.");
            return bad;
        }

        var table = GetTable();
        var entity = new TableEntity(partitionKey: "CUST", rowKey: Guid.NewGuid().ToString())
        {
            ["Name"] = dto.Name,
            ["Email"] = dto.Email,
            ["Phone"] = dto.Phone ?? ""
        };

        await table.AddEntityAsync(entity);

        var res = req.CreateResponse(HttpStatusCode.Created);
        await res.WriteAsJsonAsync(new { id = entity.RowKey, partition = entity.PartitionKey });
        return res;
    }


    [Function("Customers_Get")]
    public async Task<HttpResponseData> Get(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "customers/{partition}/{id}")]
        HttpRequestData req, string partition, string id)
    {
        var table = GetTable();
        try
        {
            var resp = await table.GetEntityAsync<TableEntity>(partition, id);
            var e = resp.Value;

            var res = req.CreateResponse(HttpStatusCode.OK);
            await res.WriteAsJsonAsync(new
            {
                id = e.RowKey,
                partition = e.PartitionKey,
                name = e.GetString("Name"),
                email = e.GetString("Email"),
                phone = e.GetString("Phone")
            });
            return res;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            var not = req.CreateResponse(HttpStatusCode.NotFound);
            await not.WriteStringAsync("Not found");
            return not;
        }
    }
}
