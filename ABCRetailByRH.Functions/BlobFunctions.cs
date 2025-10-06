using System.Net;
using System.Text.Json;
using Azure.Storage.Blobs;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ABCRetailByRH.Functions
{
    public class BlobFunctions
    {
        private readonly BlobServiceClient _blobSvc;
        private readonly ILogger _log;
        private readonly string _containerName;

        public BlobFunctions(IConfiguration cfg, ILoggerFactory lf)
        {
            var cs = cfg["AzureWebJobsStorage"] ?? throw new InvalidOperationException("Missing AzureWebJobsStorage");
            _blobSvc = new BlobServiceClient(cs);
            _containerName = cfg["BlobContainerName"] ?? "product-images";
            _log = lf.CreateLogger<BlobFunctions>();
        }

        public record UploadPayload(string? Name, string? Content);

        [Function("ProductImage_Upload")]
        public async Task<HttpResponseData> Upload([HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
        {
            var payload = await JsonSerializer.DeserializeAsync<UploadPayload>(req.Body)
                          ?? new UploadPayload(null, null);

            var container = _blobSvc.GetBlobContainerClient(_containerName);
            await container.CreateIfNotExistsAsync();

            var blobName = string.IsNullOrWhiteSpace(payload.Name)
                ? $"upload-{DateTime.UtcNow:yyyyMMddHHmmss}.txt"
                : payload.Name;

            var content = payload.Content ?? $"Uploaded at {DateTime.UtcNow:o}";
            var blob = container.GetBlobClient(blobName);
            await blob.UploadAsync(BinaryData.FromString(content), overwrite: true);

            var res = req.CreateResponse(HttpStatusCode.OK);
            await res.WriteAsJsonAsync(new { blob.Uri, blobName, container = _containerName });
            _log.LogInformation("Uploaded blob {BlobName} in {Container}", blobName, _containerName);
            return res;
        }
    }
}
