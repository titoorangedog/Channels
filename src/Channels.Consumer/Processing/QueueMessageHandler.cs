using Channels.Consumer.Abstractions;
using Channels.Consumer.Configuration;
using Channels.Consumer.Contracts;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Channels.Consumer.Processing;

public sealed class QueueMessageHandler
{
    private readonly IMessageProcessor _processor;
    private readonly IQueueClient _queueClient;
    private readonly IMessagesPersistenceStore _store;
    private readonly IDedupStore _dedupStore;
    private readonly PipelineOptions _pipelineOptions;
    private readonly string _host;
    private readonly ILogger<QueueMessageHandler> _logger;

    public QueueMessageHandler(
        IMessageProcessor processor,
        IQueueClient queueClient,
        IMessagesPersistenceStore store,
        IDedupStore dedupStore,
        IOptions<PipelineOptions> pipelineOptions,
        ILogger<QueueMessageHandler> logger)
    {
        _processor = processor;
        _queueClient = queueClient;
        _store = store;
        _dedupStore = dedupStore;
        _pipelineOptions = pipelineOptions.Value;
        _host = Environment.MachineName;
        _logger = logger;
    }

    public async Task HandleAsync(QueueReceiveItem item, CancellationToken ct)
    {
        using var scope = _logger.BeginScope(new Dictionary<string, object> { ["MessageId"] = item.MessageId });

        Exception? lastException = null;
        var maxRetries = Math.Max(1, _pipelineOptions.MaxProcessingRetries);

        for (var attempt = 1; attempt <= maxRetries; attempt++)
        {
            await _store.MarkProcessingAsync(item.MessageId, ct);

            try
            {
                var envelope = new QueueEnvelope(item.MessageId, item.Body, item.EnqueuedAt, item.Headers);
                await _processor.ProcessAsync(envelope, ct);

                if (item.NativeMessage is not null)
                {
                    await _queueClient.CompleteAsync(item, ct);
                }

                await _store.MarkCompletedAsync(item.MessageId, ct);
                _dedupStore.Complete(item.MessageId);
                return;
            }
            catch (Exception ex)
            {
                lastException = ex;
                if (attempt >= maxRetries)
                {
                    break;
                }

                var delayMs = GetBackoffDelayMs(attempt);
                _logger.LogWarning(ex, "Processing attempt {Attempt}/{MaxRetries} failed for {MessageId}.", attempt, maxRetries, item.MessageId);
                await Task.Delay(delayMs, ct);
            }
        }

        if (lastException is null)
        {
            return;
        }

        var errorEnvelope = new ErrorQueueEnvelope(
            item.MessageId,
            item.Body,
            DateTimeOffset.UtcNow,
            lastException.GetType().FullName ?? "Exception",
            lastException.Message,
            lastException.StackTrace,
            item.Headers,
            _host);

        await _queueClient.EnqueueErrorAsync(errorEnvelope, ct);

        if (item.NativeMessage is not null)
        {
            await _queueClient.CompleteAsync(item, ct);
        }

        await _store.MarkMovedToErrorAsync(item.MessageId, lastException.ToString(), ct);
        _dedupStore.Complete(item.MessageId);
    }

    private static int GetBackoffDelayMs(int attempt)
    {
        var baseDelay = attempt switch
        {
            1 => 100,
            2 => 300,
            _ => 900
        };

        var jitter = Random.Shared.Next(0, 50);
        return baseDelay + jitter;
    }
}


