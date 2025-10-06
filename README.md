# CLDV6212POEPart2ST10258492-Riaad-Hercules

# CLDV6212 POE — Part 2 (ABC Retail By RH)

**Student:** ST10258492  
**Module:** CLDV6212 — Cloud Development B

## Overview
Part 2 adds an Azure Function App to the existing ASP.NET Core MVC site to perform storage operations. Each Azure Storage type is exercised at least once, and order processing is decoupled via a queue-triggered function.

## What Was Built
- **Function App (dotnet-isolated)**
  - **HttpTrigger — Table write:** Upsert to `Customers`/`Orders`.
  - **HttpTrigger — Blob upload:** Save product images to `products` container.
  - **HttpTrigger — File upload:** Save documents to `contracts` file share.
  - **QueueTrigger — Orders_Process:** Consume `orders` queue and write to `Orders` table.
- **MVC Integration**
  - `FunctionsClient` service calls the Function App using BaseUrl + Key.
  - MVC enqueues orders; the queue trigger persists to Table Storage.
- **Storage Used**
  - Azure Tables, Blobs, Queues, and File Shares.

## How It Works (Flow)
MVC → enqueue order → `orders` queue → **QueueTrigger** → write to **Table**  
MVC → **HttpTriggers** → upload to **Blobs** / **File Share**  
Optional admin/testing via Table HttpTrigger.

## Configuration (Brief)
- **MVC:** `Functions:BaseUrl`, `Functions:Key`, `AzureStorage:*`
- **Functions:** `AzureWebJobsStorage`, `FUNCTIONS_WORKER_RUNTIME=dotnet-isolated`
- Secrets kept in user secrets / Azure App Settings (not committed).

## Run / Deploy (Brief)
1. Build both projects.
2. Start the Function App, then run the MVC app.
3. After deploy, verify: Functions running, queue drains, rows appear in Tables, blobs in container, files in share.

## Evidence Included
Screenshots of Function list, function code, queue with messages, tables (≥5 rows), blobs (≥5 items), file share items, and relevant MVC views.

## Design Notes
- Decoupled, reliable order processing via queues.
- Consistent naming conventions across storage resources.
