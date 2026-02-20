using Channels.Consumer.Contracts;

namespace Channels.Consumer.Abstractions;

public interface IQueueClient
{
    Task EnqueueMainAsync(QueueEnvelope envelope, CancellationToken ct);
    Task EnqueueErrorAsync(ErrorQueueEnvelope envelope, CancellationToken ct);
    Task<IReadOnlyList<QueuePeekItem>> PeekMainAsync(int max, CancellationToken ct);
    Task<IReadOnlyList<QueuePeekItem>> PeekErrorAsync(int max, CancellationToken ct);
    Task<QueueReceiveItem?> ReceiveMainAsync(CancellationToken ct);
    Task<QueueReceiveItem?> ReceiveErrorAsync(CancellationToken ct);
    Task CompleteAsync(QueueReceiveItem item, CancellationToken ct);
    Task AbandonAsync(QueueReceiveItem item, CancellationToken ct);
}


