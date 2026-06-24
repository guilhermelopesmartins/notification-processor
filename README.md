# Notification Processor

[![Build Status](https://dev.azure.com/guilhermelopesmartins/notification-processor/_apis/build/status%2Fguilhermelopesmartins.notification-processor?branchName=main)](https://dev.azure.com/guilhermelopesmartins/notification-processor/_build/latest?definitionId=1&branchName=main)

Serverless notification processing system built with Azure Functions, Service Bus, and SQL Server.

> Part of the roadmap: QA Analyst → Backend Developer (.NET)

---

## Architecture
[Client]

│

│ POST /api/notifications

▼

[NotificationProducer]          ← HTTP Trigger

│

│ publishes message

▼

[Azure Service Bus]             ← "notifications" queue

│

│ consumes message

▼

[NotificationConsumer]          ← Service Bus Trigger

│

│ persists via stored procedure

▼

[SQL Server]
[NotificationCleanup]           ← Timer Trigger (03:00 AM UTC)

└─ archives records older than 30 days

---

## Stack

- **Azure Functions v4** — Isolated Worker model (.NET 8)
- **Azure Service Bus** — queue with dead-letter and automatic retry
- **SQL Server** — stored procedures via Dapper
- **xUnit + NSubstitute + FluentAssertions** — unit tests

---

## Key Concepts Implemented

**Idempotency** — the Consumer checks the `MessageId` before processing. If the Service Bus redelivers a message (at-least-once delivery), the system detects the duplicate and ignores it safely.

**Dead-letter handling** — messages that fail 3 consecutive times are automatically moved to the Dead-Letter Queue by the Service Bus, without data loss.

**Async pattern** — the HTTP Trigger returns `202 Accepted` immediately after publishing to the queue. The caller doesn't wait for the notification to be processed.

**Correlation ID** — every notification carries a `CorrelationId` from entry to storage, enabling end-to-end tracing across logs.

---

## Local Setup

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Azure Functions Core Tools v4](https://learn.microsoft.com/en-us/azure/azure-functions/functions-run-local)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/)
- [Azurite](https://learn.microsoft.com/en-us/azure/storage/common/storage-use-azurite) (VS Code extension)
- Azure Service Bus namespace (Basic or Standard tier)

### 1. Start SQL Server

```bash
docker run -e "ACCEPT_EULA=Y" -e "SA_PASSWORD=YourStrong@Passw0rd" \
           -p 1433:1433 --name sqlserver-local -d \
           mcr.microsoft.com/mssql/server:2022-latest
```

### 2. Run database migration

```bash
docker cp sql/migrations/001_initial_schema.sql sqlserver-local:/001_initial_schema.sql

docker exec -it sqlserver-local /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P "YourStrong@Passw0rd" -C \
  -i /001_initial_schema.sql
```

### 3. Start Azurite

VS Code → `Ctrl+Shift+P` → `Azurite: Start`

### 4. Configure local settings

Copy and fill in your values:

```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    "ServiceBusConnectionString": "Endpoint=sb://YOUR_NAMESPACE.servicebus.windows.net/;...",
    "ServiceBusQueueName": "notifications",
    "SqlConnectionString": "Server=localhost,1433;Database=NotificationProcessorDb;User Id=sa;Password=YourStrong@Passw0rd;TrustServerCertificate=True",
    "APPLICATIONINSIGHTS_CONNECTION_STRING": ""
  }
}
```

### 5. Run

```bash
cd src/NotificationProcessor.Functions
func start
```

---

## Running Tests

```bash
dotnet test
```

---

## Architecture Decisions

**Why Isolated Worker (v4)?**
The In-Process model is deprecated. The Isolated Worker runs in a separate process, giving full control over the .NET version and dependencies without waiting for the Functions host to update.

**Why Dapper instead of EF Core?**
The project uses stored procedures, which is the standard in corporate environments with SQL Server. Dapper provides direct control over SQL execution with minimal overhead. EF Core would be more appropriate for greenfield projects without existing procedures.

**Why `202 Accepted` in the Producer?**
The HTTP Trigger doesn't know whether the notification will be delivered successfully — that's the Consumer's responsibility. Returning `202` communicates that the request was accepted and is being processed asynchronously. Returning `200` would imply the notification was already sent, which would be inaccurate.

**Why manual message completion?**
With `autoCompleteMessages: false` in `host.json`, the Consumer explicitly calls `CompleteMessageAsync` only after successfully persisting to the database. This prevents silent message loss if the function completes but the database write fails.