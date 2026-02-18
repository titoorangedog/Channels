# Channels solution (.NET 10)

Refactor in **2 progetti separati** per poterli avviare in istanze diverse:

1. **QueueService** (Web API) → mantiene le code in memoria con `System.Threading.Channels`
2. **ReportConsumer** (Worker) → consuma un messaggio alla volta chiamando QueueService

## Queue logiche

- `BackOfficeEU.Reports` (main queue)
- `BackOfficeEU.Reports.Error` (error queue)

## Flusso

1. Il client invia JSON a `QueueService` con `POST /api/reports/enqueue`.
2. `ReportConsumer` fa polling `POST /api/reports/dequeue` e prende un solo messaggio.
3. Se la lavorazione va a buon fine, il messaggio è già rimosso dalla queue principale.
4. Se fallisce, il consumer invia `POST /api/reports/fail` e il messaggio viene spostato nella queue errori.

## Endpoint QueueService

- `POST /api/reports/enqueue`
- `POST /api/reports/dequeue`
- `POST /api/reports/fail`
- `GET /api/reports/errors`
- `POST /api/reports/errors/{messageId}/requeue`

## Esecuzione separata

Terminale 1:

```bash
dotnet run --project src/QueueService/QueueService.csproj
```

Terminale 2:

```bash
dotnet run --project src/ReportConsumer/ReportConsumer.csproj
```

## Esempio payload enqueue

```json
{
  "id": "msg-001",
  "reportId": "report-sales",
  "className": "Guess.Reports.SalesReport",
  "user": "backoffice-eu",
  "executionCount": 0,
  "data": {
    "country": "IT",
    "forceError": "false"
  }
}
```
