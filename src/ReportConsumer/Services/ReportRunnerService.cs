using ReportConsumer.Models;

namespace ReportConsumer.Services;

public sealed class ReportRunnerService(ILogger<ReportRunnerService> logger) : IReportRunnerService
{
    public async Task ExecuteReportAsync(ReportExecutionModel model, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(model);

        logger.LogInformation(
            "ExecuteReport - ClassName: {ClassName}, User: {User}, ReportId: {ReportId}, MessageId: {Id}",
            model.ClassName,
            model.User,
            model.ReportId,
            model.Id);

        await Task.Delay(250, cancellationToken);

        if (model.Data?.TryGetValue("forceError", out var forceError) is true
            && bool.TryParse(forceError, out var mustFail)
            && mustFail)
        {
            throw new InvalidOperationException($"Report {model.ReportId} forzato in errore (forceError=true).");
        }

        logger.LogInformation("Messaggio {Id} processato con successo", model.Id);
    }
}
