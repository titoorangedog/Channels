using Channels.Api.Abstractions;
using Channels.Api.Contracts;

namespace Channels.Api.Queue;

public sealed class InMemoryQueueClient : IQueueClient
{
    private readonly object _sync = new();
    private readonly Queue<QueueData> _mainQueue = new();
    private readonly Queue<QueueData> _errorQueue = new();
    private readonly Dictionary<Guid, QueueData> _inflight = new();
    private readonly IMessageSerializer _serializer;

    public InMemoryQueueClient(IMessageSerializer serializer)
    {
        _serializer = serializer;
    }

    public Task EnqueueMainAsync(QueueEnvelope envelope, CancellationToken ct)
    {
        lock (_sync)
        {
            _mainQueue.Enqueue(new QueueData
            {
                Token = Guid.NewGuid(),
                MessageId = envelope.MessageId,
                Payload = envelope.Payload,
                Headers = _serializer.NormalizeHeaders(envelope.Headers),
                EnqueuedAt = envelope.EnqueuedAt,
                QueueName = "BackOfficeEU.Reports"
            });
        }

        return Task.CompletedTask;
    }

    public Task EnqueueErrorAsync(ErrorQueueEnvelope envelope, CancellationToken ct)
    {
        lock (_sync)
        {
            _errorQueue.Enqueue(new QueueData
            {
                Token = Guid.NewGuid(),
                MessageId = envelope.OriginalMessageId,
                Payload = _serializer.Serialize(envelope),
                Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["ErrorEnvelope"] = "true"
                },
                EnqueuedAt = envelope.FailedAt,
                QueueName = "BackOfficeEU.Reports.Error"
            });
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<QueuePeekItem>> PeekMainAsync(int max, CancellationToken ct)
    {
        lock (_sync)
        {
            return Task.FromResult<IReadOnlyList<QueuePeekItem>>(_mainQueue.Take(max)
                .Select(MapPeek)
                .ToList());
        }
    }

    public Task<IReadOnlyList<QueuePeekItem>> PeekErrorAsync(int max, CancellationToken ct)
    {
        lock (_sync)
        {
            return Task.FromResult<IReadOnlyList<QueuePeekItem>>(_errorQueue.Take(max)
                .Select(MapPeek)
                .ToList());
        }
    }

    public Task<QueueReceiveItem?> ReceiveMainAsync(CancellationToken ct)
    {
        lock (_sync)
        {
            if (_mainQueue.Count == 0)
            {
                return Task.FromResult<QueueReceiveItem?>(null);
            }

            var data = _mainQueue.Dequeue();
            _inflight[data.Token] = data;
            return Task.FromResult<QueueReceiveItem?>(MapReceive(data));
        }
    }

    public Task<QueueReceiveItem?> ReceiveErrorAsync(CancellationToken ct)
    {
        lock (_sync)
        {
            if (_errorQueue.Count == 0)
            {
                return Task.FromResult<QueueReceiveItem?>(null);
            }

            var data = _errorQueue.Dequeue();
            _inflight[data.Token] = data;
            return Task.FromResult<QueueReceiveItem?>(MapReceive(data));
        }
    }

    public Task CompleteAsync(QueueReceiveItem item, CancellationToken ct)
    {
        if (item.NativeMessage is not Guid token)
        {
            return Task.CompletedTask;
        }

        lock (_sync)
        {
            _inflight.Remove(token);
        }

        return Task.CompletedTask;
    }

    public Task AbandonAsync(QueueReceiveItem item, CancellationToken ct)
    {
        if (item.NativeMessage is not Guid token)
        {
            return Task.CompletedTask;
        }

        lock (_sync)
        {
            if (!_inflight.Remove(token, out var data))
            {
                return Task.CompletedTask;
            }

            if (data.QueueName == "BackOfficeEU.Reports.Error")
            {
                _errorQueue.Enqueue(data);
            }
            else
            {
                _mainQueue.Enqueue(data);
            }
        }

        return Task.CompletedTask;
    }

    private static QueuePeekItem MapPeek(QueueData data)
    {
        return new QueuePeekItem(
            data.MessageId,
            data.EnqueuedAt,
            new Dictionary<string, string>(data.Headers, StringComparer.OrdinalIgnoreCase),
            data.Payload);
    }

    private static QueueReceiveItem MapReceive(QueueData data)
    {
        return new QueueReceiveItem
        {
            MessageId = data.MessageId,
            Body = data.Payload,
            Headers = new Dictionary<string, string>(data.Headers, StringComparer.OrdinalIgnoreCase),
            EnqueuedAt = data.EnqueuedAt,
            NativeMessage = data.Token,
            QueueName = data.QueueName
        };
    }

    private sealed class QueueData
    {
        public Guid Token { get; set; }
        public string MessageId { get; set; } = string.Empty;
        public string Payload { get; set; } = string.Empty;
        public IDictionary<string, string> Headers { get; set; } = new Dictionary<string, string>();
        public DateTimeOffset EnqueuedAt { get; set; }
        public string QueueName { get; set; } = string.Empty;
    }
}
