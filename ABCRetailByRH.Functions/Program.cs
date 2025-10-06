using System;
using System.IO;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Azure.Storage.Files.Shares;
using Azure.Storage.Queues;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ABCRetailByRH.Functions
{
    public class StorageClients
    {
        public TableServiceClient Tables { get; }
        public BlobServiceClient Blobs { get; }
        public QueueServiceClient Queues { get; }
        public ShareServiceClient Shares { get; }

        public StorageClients(IConfiguration config)
        {
            // Which key holds our storage connection? (defaults to AzureWebJobsStorage)
            var settingName =
                config["StorageConnection"] ??
                config["Values:StorageConnection"] ??
                "AzureWebJobsStorage";

            string? conn;

            // If they pasted a full connection string into StorageConnection, use it directly
            if (settingName.Contains("AccountName=", StringComparison.OrdinalIgnoreCase) ||
                settingName.StartsWith("DefaultEndpointsProtocol=", StringComparison.OrdinalIgnoreCase))
            {
                conn = settingName;
            }
            else
            {
                // Otherwise resolve by name from all likely locations
                conn =
                    config[settingName] ??                         // direct key
                    config[$"Values:{settingName}"] ??             // Values:AzureWebJobsStorage
                    config.GetConnectionString(settingName) ??     // ConnectionStrings:AzureWebJobsStorage
                    Environment.GetEnvironmentVariable(settingName); // env var
            }

            if (string.IsNullOrWhiteSpace(conn))
                throw new InvalidOperationException($"{settingName} is missing.");

            Tables = new TableServiceClient(conn);
            Blobs = new BlobServiceClient(conn);
            Queues = new QueueServiceClient(conn);
            Shares = new ShareServiceClient(conn);
        }
    }

    public class Program
    {
        public static void Main()
        {
            var host = new HostBuilder()
                .ConfigureFunctionsWorkerDefaults()
                .ConfigureAppConfiguration(cfg =>
                {
                    // Base path = app directory (bin/Debug/...)
                    var baseDir = AppContext.BaseDirectory;
                    cfg.SetBasePath(baseDir);

                    // Load local.settings.json from bin (after we set CopyToOutput) …
                    cfg.AddJsonFile("local.settings.json", optional: true, reloadOnChange: true);

                    // …and ALSO try the project root (three levels up) in case it wasn’t copied
                    var rootLocal = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "local.settings.json"));
                    if (File.Exists(rootLocal))
                        cfg.AddJsonFile(rootLocal, optional: true, reloadOnChange: true);

                    // Include User Secrets (VS sometimes stores Values there)
                    cfg.AddUserSecrets<Program>(optional: true);

                    // Environment variables (Functions host maps Values -> env vars)
                    cfg.AddEnvironmentVariables();
                })
                .ConfigureServices(s => s.AddSingleton<StorageClients>())
                .Build();

            host.Run();
        }
    }
}
