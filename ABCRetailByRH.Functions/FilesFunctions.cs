using System.Net;
using System.Text;
using System.Text.Json;
using Azure.Storage.Files.Shares;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ABCRetailByRH.Functions
{
    public class FilesFunctions
    {
        private readonly ShareServiceClient _shareSvc;
        private readonly ILogger _log;
        private readonly string _shareName;

        public FilesFunctions(IConfiguration cfg, ILoggerFactory lf)
        {
            var cs = cfg["AzureWebJobsStorage"] ?? throw new InvalidOperationException("Missing AzureWebJobsStorage");
            _shareSvc = new ShareServiceClient(cs);
            _shareName = cfg["ContractsShareName"] ?? "contracts";
            _log = lf.CreateLogger<FilesFunctions>();
        }

        public record FilePayload(string? Name, string? Content);

        [Function("Contracts_SaveToFiles")]
        public async Task<HttpResponseData> SaveToFiles([HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
        {
            var payload = await JsonSerializer.DeserializeAsync<FilePayload>(req.Body)
                          ?? new FilePayload(null, null);

            var share = _shareSvc.GetShareClient(_shareName);
            await share.CreateIfNotExistsAsync();
            var root = share.GetRootDirectoryClient();

            var fileName = string.IsNullOrWhiteSpace(payload.Name)
                ? $"contract-{DateTime.UtcNow:yyyyMMddHHmmss}.txt"
                : payload.Name;

            var file = root.GetFileClient(fileName);
            if (await file.ExistsAsync()) await file.DeleteAsync();

            var bytes = Encoding.UTF8.GetBytes(payload.Content ?? $"Saved at {DateTime.UtcNow:o}");
            using var ms = new MemoryStream(bytes);

            await file.CreateAsync(ms.Length);
            ms.Position = 0;
            await file.UploadAsync(ms);

            var res = req.CreateResponse(HttpStatusCode.OK);
            await res.WriteAsJsonAsync(new { share = _shareName, file = fileName });
            _log.LogInformation("Wrote file {File} to share {Share}", fileName, _shareName);
            return res;
        }
    }
}
