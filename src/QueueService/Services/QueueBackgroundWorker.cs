using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace QueueService.Services
{
    // Optional background worker — can perform maintenance, auto-retries, metrics.
    public sealed class QueueBackgroundWorker : BackgroundService
    {
        private readonly IReportQueueService _queue;
        private readonly ILogger<QueueBackgroundWorker> _logger;

        public QueueBackgroundWorker(IReportQueueService queue, ILogger<QueueBackgroundWorker> logger)
        {
            _queue = queue;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("QueueBackgroundWorker started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Example: periodically log errors snapshot
                    var errors = _queue.SnapshotErrors();
                    _logger.LogDebug("Current error count: {Count}", errors.Count);

                    await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected error in QueueBackgroundWorker; continuing.");
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                }
            }

            _logger.LogInformation("QueueBackgroundWorker stopping.");
        }
    }
}
