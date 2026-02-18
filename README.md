# ChannelsApi (.NET 10)

Sostituisce RabbitMQ con `System.Threading.Channels` mantenendo due code logiche:

- `BackOfficeEU.Reports` (queue principale)
- `BackOfficeEU.Reports.Error` (queue errori)

## Comportamento

1. L'API riceve un JSON (`POST /api/reports/enqueue`) e lo inserisce nella queue principale.
2. Un `BackgroundService` consuma **un messaggio alla volta** (`SingleReader=true`).
3. Se il processing ha successo, il messaggio viene considerato completato (rimosso).
4. Se il processing fallisce, il payload viene spostato nella error queue.

## Endpoint

- `POST /api/reports/enqueue`
- `GET /api/reports/errors`
- `POST /api/reports/errors/{messageId}/requeue`

## Esempio payload

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

Se `data.forceError=true`, il `ReportRunnerService` genera una failure simulata e il messaggio finisce nella queue errori.
