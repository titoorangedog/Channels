using Channels.Consumer.Persistence;
using Channels.Consumer.Abstractions;
using Channels.Consumer.Configuration;
using Channels.Consumer.Contracts;
using Channels.Api.Persistence;
using Channels.Producer.Configuration;
using Microsoft.Extensions.Options;

namespace Channels.Api.Services;

public sealed class QueueMoveService
{
    private readonly IQueueClient _queueClient;
    private readonly IMessageSerializer _serializer;
    private readonly IMessagesPersistenceStore _store;
    private readonly QueueOptions _queueOptions;
    private readonly PipelineOptions _pipelineOptions;

    public QueueMoveService(
        IQueueClient queueClient,
        IMessageSerializer serializer,
        IMessagesPersistenceStore store,
        IOptions<QueueOptions> queueOptions,
        IOptions<PipelineOptions> pipelineOptions)
    {
        _queueClient = queueClient;
        _serializer = serializer;
        _store = store;
        _queueOptions = queueOptions.Value;
        _pipelineOptions = pipelineOptions.Value;
    }

    public async Task<bool> MoveByIdAsync(string messageId, CancellationToken ct)
    {
        var scanLimit = Math.Max(1, _pipelineOptions.ErrorMoveScanLimit);

        for (var scanned = 0; scanned < scanLimit; scanned++)
        {
            var received = await _queueClient.ReceiveErrorAsync(ct);
            if (received is null)
            {
                return false;
            }

            var errorEnvelope = TryParseErrorEnvelope(received);
            var matches = string.Equals(received.MessageId, messageId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(errorEnvelope?.OriginalMessageId, messageId, StringComparison.OrdinalIgnoreCase);

            if (!matches)
            {
                // Broker ordering is not strict; abandoning reduces mutation but may resurface the same message.
                await _queueClient.AbandonAsync(received, ct);
                continue;
            }

            var envelope = ToMainEnvelope(received, errorEnvelope);
            await _queueClient.EnqueueMainAsync(envelope, ct);
            await _queueClient.CompleteAsync(received, ct);
            await UpsertPendingAsync(envelope, ct);
            return true;
        }

        return false;
    }

    public async Task<int> MoveAllAsync(CancellationToken ct)
    {
        var moved = 0;

        while (true)
        {
            var received = await _queueClient.ReceiveErrorAsync(ct);
            if (received is null)
            {
                break;
            }

            var errorEnvelope = TryParseErrorEnvelope(received);
            var envelope = ToMainEnvelope(received, errorEnvelope);
            await _queueClient.EnqueueMainAsync(envelope, ct);
            await _queueClient.CompleteAsync(received, ct);
            await UpsertPendingAsync(envelope, ct);
            moved++;
        }

        return moved;
    }

    private async Task UpsertPendingAsync(QueueEnvelope envelope, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        await _store.UpsertAsync(new PersistedMessageDocument
        {
            Id = envelope.MessageId,
            QueueName = _queueOptions.QueueName,
            Payload = envelope.Payload,
            Headers = new Dictionary<string, string>(envelope.Headers, StringComparer.OrdinalIgnoreCase),
            EnqueuedAt = envelope.EnqueuedAt,
            CreatedAt = now,
            Status = "Pending",
            ExpiresAt = now.AddDays(MongoOptions.RetentionDays)
        }, ct);
    }

    private ErrorQueueEnvelope? TryParseErrorEnvelope(QueueReceiveItem item)
    {
        try
        {
            return _serializer.Deserialize<ErrorQueueEnvelope>(item.Body);
        }
        catch
        {
            return null;
        }
    }

    private static QueueEnvelope ToMainEnvelope(QueueReceiveItem item, ErrorQueueEnvelope? errorEnvelope)
    {
        if (errorEnvelope is null)
        {
            return new QueueEnvelope(item.MessageId, item.Body, DateTimeOffset.UtcNow, item.Headers);
        }

        return new QueueEnvelope(
            errorEnvelope.OriginalMessageId,
            errorEnvelope.OriginalPayload,
            DateTimeOffset.UtcNow,
            new Dictionary<string, string>(errorEnvelope.OriginalHeaders, StringComparer.OrdinalIgnoreCase));
    }
}


