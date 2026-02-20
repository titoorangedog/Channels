# Channels - Minimal API Queue Processing (.NET 10)

## Architecture
- `src/Channels.Api`: ASP.NET Core Minimal API.
- `tests/Channels.Api.Tests`: xUnit tests with in-memory queue/persistence.

Internal pipeline:
1. `ProducerBackgroundService` receives from the main queue (`PeekLock` on Azure Service Bus), persists to Mongo (`Pending`), and publishes to a bounded `Channel<QueueReceiveItem>`.
2. `ConsumerPoolBackgroundService` starts 1..N workers that read from the channel.
3. `QueueMessageHandler` applies retries with exponential backoff + jitter; on definitive failure it publishes to the error queue, completes the main lock, and marks Mongo as `MovedToError`.

Mongo persistence:
- Collection with 30-day TTL on `ExpiresAt` (`expireAfterSeconds=0`).
- No message enters the channel without a successful upsert first.
- On restart, `Pending/Processing` records are reloaded into the channel.
- Dedup by `MessageId` (`IDedupStore`) prevents uncontrolled duplicates under at-least-once semantics.

## Minimal API Endpoints
- `POST /api/reports/enqueue`
  Enqueue a `ReportExecutionModel` message into the main queue.
- `GET /api/queues/main/messages?max=100`
  Peek messages from the main queue without consuming them.
- `GET /api/queues/error/messages?max=100`
  Peek messages from the error queue without consuming them.
- `POST /api/queues/error/move/{messageId}`
  Move a single message from error queue to main queue by `messageId`.
- `POST /api/queues/error/move-all`
  Move all messages from error queue to main queue.

## Configuration
`appsettings.json`
- `Queue` (Provider, ConnectionString, QueueName, QueueErrorName)
- `Pipeline` (ChannelCapacity, ConsumerCount, MaxProcessingRetries, ShutdownDrainTimeoutSeconds, ReceiveWaitTimeMs, PeekMaxDefault, ErrorMoveScanLimit)
- `Mongo` (ConnectionString, DatabaseName, CollectionName, TtlDays)

For local test/demo use `appsettings.Development.json`:
- `Queue.Provider = InMemory`
- `Mongo.ConnectionString = InMemory`

## Commands
```bash
dotnet test
dotnet run --project src/Channels.Api
```
