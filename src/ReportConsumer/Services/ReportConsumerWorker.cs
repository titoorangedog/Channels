using Microsoft.Extensions.Options;
using ReportConsumer.Configuration;
using ReportConsumer.Models;

namespace ReportConsumer.Services;

public sealed class ReportConsumerWorker(
    QueueServiceClient queueClient,
    IReportRunnerService reportRunner,
    IOptions<ConsumerOptions> options,
    ILogger<ReportConsumerWorker> logger) : BackgroundService
{
    private readonly ConsumerOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Report consumer worker avviato.");

        while (!stoppingToken.IsCancellationRequested)
        {
            var dequeued = await queueClient.TryDequeueAsync(stoppingToken);
            if (dequeued is null || !dequeued.HasMessage || dequeued.Message is null)
            {
                await Task.Delay(_options.EmptyQueueDelayMs, stoppingToken);
                continue;
            }

            await ProcessSingleMessageAsync(dequeued.Message, stoppingToken);
        }
    }

    private async Task ProcessSingleMessageAsync(ReportExecutionModel message, CancellationToken cancellationToken)
    {
        try
        {
            await reportRunner.ExecuteReportAsync(message, cancellationToken);
        }
        catch (Exception ex)
        {
            await queueClient.SendFailAsync(
                new FailMessageRequest(message, ex.Message, ex.GetType().FullName),
                cancellationToken);

            logger.LogWarning(ex, "Messaggio {MessageId} fallito e spostato nella error queue", message.Id);
        }
    }
}
