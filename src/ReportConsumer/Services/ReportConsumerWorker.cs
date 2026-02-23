using System.Net.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReportConsumer.Configuration;
using ReportConsumer.Models;

namespace ReportConsumer.Services;

public sealed class ReportConsumerWorker : BackgroundService
{
    private readonly QueueServiceClient _queueClient;
    private readonly IReportRunnerService _reportRunner;
    private readonly ILogger<ReportConsumerWorker> _logger;
    private readonly ConsumerOptions _options;

    public ReportConsumerWorker(
        QueueServiceClient queueClient,
        IReportRunnerService reportRunner,
        IOptions<ConsumerOptions> options,
        ILogger<ReportConsumerWorker> logger)
    {
        _queueClient = queueClient;
        _reportRunner = reportRunner;
        _logger = logger;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Report consumer worker avviato.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var message = await _queueClient.TryDequeueAsync(stoppingToken);

                if (message is null)
                {
                    // nothing available — short backoff
                    await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
                    continue;
                }

                await _reportRunner.ExecuteReportAsync(message, stoppingToken);
            }
            catch (HttpRequestException ex)
            {
                // transient network error — log and retry after delay
                _logger.LogWarning(ex, "QueueService unreachable; retrying in 5s.");
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in background worker; continuing after 5s delay.");
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }

        _logger.LogInformation("Report consumer worker stopping.");
    }
}
