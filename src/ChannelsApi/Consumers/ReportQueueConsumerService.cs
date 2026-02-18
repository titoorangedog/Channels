using ChannelsApi.Models;
using ChannelsApi.Services;

namespace ChannelsApi.Consumers;

public sealed class ReportQueueConsumerService(
    IReportQueueService queueService,
    IReportRunnerService reportRunner,
    ILogger<ReportQueueConsumerService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Report queue consumer avviato (single message consumer).");

        await foreach (var message in queueService.ReadMainQueueAsync(stoppingToken))
        {
            await ProcessSingleMessageAsync(message, stoppingToken);
        }
    }

    private async Task ProcessSingleMessageAsync(ReportExecutionModel message, CancellationToken cancellationToken)
    {
        try
        {
            await reportRunner.ExecuteReportAsync(message, cancellationToken);
            logger.LogInformation("Messaggio {MessageId} processato con successo e rimosso.", message.Id);
        }
        catch (Exception ex)
        {
            var errorMessage = new ErrorQueueMessage(
                Payload: message,
                ErrorMessage: ex.Message,
                FailedAt: DateTimeOffset.UtcNow,
                ExceptionType: ex.GetType().FullName
            );

            await queueService.EnqueueErrorAsync(errorMessage, cancellationToken);
        }
    }
}
