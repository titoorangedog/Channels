using Channels.Consumer.Persistence;

namespace Channels.Consumer.Abstractions;

public interface IMessagesPersistenceStore
{
    Task UpsertAsync(PersistedMessageDocument doc, CancellationToken ct);
    Task MarkProcessingAsync(string messageId, CancellationToken ct);
    Task MarkCompletedAsync(string messageId, CancellationToken ct);
    Task MarkMovedToErrorAsync(string messageId, string error, CancellationToken ct);
    Task<IReadOnlyList<PersistedMessageDocument>> LoadUnfinishedAsync(CancellationToken ct);
    Task<Dictionary<string, string>> GetStatusesAsync(IEnumerable<string> messageIds, CancellationToken ct);
    Task<bool> ExistsUnfinishedAsync(string messageId, CancellationToken ct);
}


