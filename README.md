# Channels - Minimal API Queue Processing (.NET 10)

## Architettura
- `src/Channels.Api`: Minimal API ASP.NET Core.
- `tests/Channels.Api.Tests`: test xUnit con queue/persistence in-memory.

Pipeline interna:
1. `ProducerBackgroundService` riceve dalla main queue (`PeekLock` su Azure Service Bus), persiste su Mongo (`Pending`) e pubblica su `Channel<QueueReceiveItem>` bounded.
2. `ConsumerPoolBackgroundService` avvia 1..N worker che leggono dal channel.
3. `QueueMessageHandler` applica retry con backoff esponenziale + jitter; su fallimento definitivo pubblica in error queue, completa il lock della main e marca Mongo `MovedToError`.

Persistenza Mongo:
- Collection con TTL 30 giorni su `ExpiresAt` (`expireAfterSeconds=0`).
- Nessun messaggio entra nel channel senza upsert riuscito.
- Al riavvio i record `Pending/Processing` vengono reinseriti nel channel.
- Dedup su `MessageId` (`IDedupStore`) evita duplicazioni incontrollate in semantica at-least-once.

## Endpoint
- `POST /api/reports/enqueue`
- `GET /api/queues/main/messages?max=100`
- `GET /api/queues/error/messages?max=100`
- `POST /api/queues/error/move/{messageId}`
- `POST /api/queues/error/move-all`

## Configurazione
`appsettings.json`
- `Queue` (Provider, ConnectionString, QueueName, QueueErrorName)
- `Pipeline` (ChannelCapacity, ConsumerCount, MaxProcessingRetries, ShutdownDrainTimeoutSeconds, ReceiveWaitTimeMs, PeekMaxDefault, ErrorMoveScanLimit)
- `Mongo` (ConnectionString, DatabaseName, CollectionName, TtlDays)

Per test/demo locale usa `appsettings.Development.json`:
- `Queue.Provider = InMemory`
- `Mongo.ConnectionString = InMemory`

## Comandi
```bash
dotnet test
dotnet run --project src/Channels.Api
```
