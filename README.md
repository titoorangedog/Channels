
# Channels (multi-progetto) — .NET 10

Repository multi-servizio per la gestione e il consumo di messaggi di report usando `System.Threading.Channels`.

Progetti principali presenti in `src/`:

- `ChannelsApi` — API Web per accodare report e gestire la queue di errori (`Controllers/ReportsController`).
- `QueueService` — servizio HTTP che espone una queue mockata/local per testing.
- `ReportConsumer` — Worker Service che consuma la queue e esegue i report (`BackgroundService`).

Architettura e comportamento

- Due queue logiche:
  - `BackOfficeEU.Reports` (queue principale)
  - `BackOfficeEU.Reports.Error` (queue errori)
- Flusso:
  1. L'API accetta un JSON in `POST /api/reports/enqueue` e lo inserisce nella queue principale.
  2. Il consumer (`ReportConsumer`) legge un messaggio alla volta.
  3. Se il processing ha successo il messaggio viene rimosso.
  4. Se il processing fallisce il messaggio viene spostato nella error queue.

Endpoint disponibili

- `POST /api/reports/enqueue` — accoda un report.
- `GET /api/reports/errors` — ritorna lo snapshot dei messaggi nella error queue.
- `POST /api/reports/errors/{messageId}/requeue` — reinserisce un messaggio dalla error queue nella queue principale.

Esempio payload per `enqueue`

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

Nota: se `data.forceError` è `true`, il `ReportRunnerService` simula un errore e il messaggio viene spostato nella error queue.

Prerequisiti

- .NET 10 SDK
- Git

Build

Dalla root del repository:

```bash
dotnet restore
dotnet build
```

Esecuzione

Eseguire i progetti singolarmente (in terminali separati):

```bash
dotnet run --project src/ChannelsApi
dotnet run --project src/QueueService
dotnet run --project src/ReportConsumer
```

Note su Swagger

I progetti web (`ChannelsApi` e `QueueService`) espongono la documentazione Swagger quando l'ambiente è `Development`. Aprire l'URL indicato nella console (tipicamente `http://localhost:5xxx/swagger`).

Configurazione

Le impostazioni sono lette tramite le classi di opzioni (`QueueOptions`, `ConsumerOptions`) e `appsettings.json` nei rispettivi progetti.

Testing rapido

- Per accodare un report: `POST /api/reports/enqueue` con il payload JSON sopra.
- Per visualizzare errori: `GET /api/reports/errors`.
- Per reinserire un messaggio di errore: `POST /api/reports/errors/{messageId}/requeue`.

Altre note

- `ReportConsumer` è implementato come `BackgroundService`.
- Se mancano pacchetti o estensioni, eseguire `dotnet restore` e ricostruire il progetto.

Se vuoi modifiche specifiche al README (lingua, contenuti o dettagli di deployment), dimmi cosa aggiornare.
