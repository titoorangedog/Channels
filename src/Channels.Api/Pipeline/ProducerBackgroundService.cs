using System.Threading.Channels;
using Channels.Api.Abstractions;
using Channels.Api.Configuration;
using Channels.Api.Contracts;
using Channels.Api.Persistence;
using Microsoft.Extensions.Options;

namespace Channels.Api.Pipeline;

public sealed class ProducerBackgroundService : IHostedService
{
    private readonly IQueueClient _queueClient;
    private readonly IMessagesPersistenceStore _store;
    private readonly IDedupStore _dedupStore;
    private readonly Channel<QueueReceiveItem> _channel;
    private readonly QueueOptions _queueOptions;
    private readonly MongoOptions _mongoOptions;
    private readonly ILogger<ProducerBackgroundService> _logger;

    private CancellationTokenSource? _cts;
    private Task? _runTask;

    public ProducerBackgroundService(
        IQueueClient queueClient,
        IMessagesPersistenceStore store,
        IDedupStore dedupStore,
        Channel<QueueReceiveItem> channel,
        IOptions<QueueOptions> queueOptions,
        IOptions<MongoOptions> mongoOptions,
        ILogger<ProducerBackgroundService> logger)
    {
        _queueClient = queueClient;
        _store = store;
        _dedupStore = dedupStore;
        _channel = channel;
        _queueOptions = queueOptions.Value;
        _mongoOptions = mongoOptions.Value;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _runTask = Task.Run(() => RunAsync(_cts.Token), CancellationToken.None);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_cts is null)
        {
            return;
        }

        _cts.Cancel();

        if (_runTask is not null)
        {
            await Task.WhenAny(_runTask, Task.Delay(Timeout.Infinite, cancellationToken));
        }

        _channel.Writer.TryComplete();
    }

    private async Task RunAsync(CancellationToken ct)
    {
        try
        {
            await RecoverUnfinishedAsync(ct);

            while (!ct.IsCancellationRequested)
            {
                var message = await _queueClient.ReceiveMainAsync(ct);
                if (message is null)
                {
                    continue;
                }

                var exists = await _store.ExistsUnfinishedAsync(message.MessageId, ct);
                if (exists)
                {
                    // At-least-once delivery can duplicate live receives after restart.
                    await _queueClient.CompleteAsync(message, ct);
                    continue;
                }

                var now = DateTimeOffset.UtcNow;
                var doc = new PersistedMessageDocument
                {
                    Id = message.MessageId,
                    QueueName = _queueOptions.QueueName,
                    Payload = message.Body,
                    Headers = new Dictionary<string, string>(message.Headers, StringComparer.OrdinalIgnoreCase),
                    EnqueuedAt = message.EnqueuedAt,
                    CreatedAt = now,
                    Status = "Pending",
                    ExpiresAt = now.AddDays(_mongoOptions.TtlDays)
                };

                try
                {
                    await _store.UpsertAsync(doc, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Persistence upsert failed for {MessageId}; abandoning message lock.", message.MessageId);
                    await _queueClient.AbandonAsync(message, ct);
                    continue;
                }

                if (!_dedupStore.TryStart(message.MessageId, TimeSpan.FromHours(2)))
                {
                    await _queueClient.CompleteAsync(message, ct);
                    continue;
                }

                await _channel.Writer.WriteAsync(message, ct);
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            _channel.Writer.TryComplete();
        }
    }

    private async Task RecoverUnfinishedAsync(CancellationToken ct)
    {
        var unfinished = await _store.LoadUnfinishedAsync(ct);
        foreach (var doc in unfinished)
        {
            if (!_dedupStore.TryStart(doc.Id, TimeSpan.FromHours(2)))
            {
                continue;
            }

            var item = new QueueReceiveItem
            {
                MessageId = doc.Id,
                Body = doc.Payload,
                Headers = new Dictionary<string, string>(doc.Headers, StringComparer.OrdinalIgnoreCase),
                EnqueuedAt = doc.EnqueuedAt,
                NativeMessage = null,
                QueueName = doc.QueueName
            };

            await _channel.Writer.WriteAsync(item, ct);
        }
    }
}
