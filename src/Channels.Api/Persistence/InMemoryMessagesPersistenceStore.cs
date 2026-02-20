using Channels.Consumer.Persistence;
using Channels.Consumer.Abstractions;

namespace Channels.Api.Persistence;

public sealed class InMemoryMessagesPersistenceStore : IMessagesPersistenceStore
{
    private readonly Dictionary<string, PersistedMessageDocument> _items = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _sync = new();

    public Task UpsertAsync(PersistedMessageDocument doc, CancellationToken ct)
    {
        lock (_sync)
        {
            _items[doc.Id] = Clone(doc);
        }

        return Task.CompletedTask;
    }

    public Task MarkProcessingAsync(string messageId, CancellationToken ct)
    {
        lock (_sync)
        {
            if (_items.TryGetValue(messageId, out var doc))
            {
                doc.Status = "Processing";
                doc.LastAttemptAt = DateTimeOffset.UtcNow;
                doc.AttemptCount++;
            }
        }

        return Task.CompletedTask;
    }

    public Task MarkMovedToErrorAsync(string messageId, string error, CancellationToken ct)
    {
        lock (_sync)
        {
            if (_items.TryGetValue(messageId, out var doc))
            {
                doc.Status = "MovedToError";
                doc.LastError = error;
                doc.LastAttemptAt = DateTimeOffset.UtcNow;
            }
        }

        return Task.CompletedTask;
    }

    public Task DeleteAsync(string messageId, CancellationToken ct)
    {
        lock (_sync)
        {
            _items.Remove(messageId);
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<PersistedMessageDocument>> LoadUnfinishedAsync(CancellationToken ct)
    {
        lock (_sync)
        {
            var docs = _items.Values
                .Where(x => x.QueueName == "BackOfficeEU.Reports" && (x.Status == "Pending" || x.Status == "Processing"))
                .Select(Clone)
                .ToList();

            return Task.FromResult<IReadOnlyList<PersistedMessageDocument>>(docs);
        }
    }

    public Task<Dictionary<string, string>> GetStatusesAsync(IEnumerable<string> messageIds, CancellationToken ct)
    {
        lock (_sync)
        {
            var map = messageIds.Distinct(StringComparer.OrdinalIgnoreCase)
                .Where(x => _items.ContainsKey(x))
                .ToDictionary(x => x, x => _items[x].Status, StringComparer.OrdinalIgnoreCase);

            return Task.FromResult(map);
        }
    }

    public Task<bool> ExistsUnfinishedAsync(string messageId, CancellationToken ct)
    {
        lock (_sync)
        {
            var exists = _items.TryGetValue(messageId, out var doc)
                && (doc.Status == "Pending" || doc.Status == "Processing");

            return Task.FromResult(exists);
        }
    }

    private static PersistedMessageDocument Clone(PersistedMessageDocument doc)
    {
        return new PersistedMessageDocument
        {
            Id = doc.Id,
            QueueName = doc.QueueName,
            Payload = doc.Payload,
            Headers = new Dictionary<string, string>(doc.Headers, StringComparer.OrdinalIgnoreCase),
            EnqueuedAt = doc.EnqueuedAt,
            CreatedAt = doc.CreatedAt,
            LastAttemptAt = doc.LastAttemptAt,
            AttemptCount = doc.AttemptCount,
            Status = doc.Status,
            LastError = doc.LastError,
            ExpiresAt = doc.ExpiresAt
        };
    }
}


