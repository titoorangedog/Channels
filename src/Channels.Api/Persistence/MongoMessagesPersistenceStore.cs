using Channels.Api.Abstractions;
using Channels.Api.Configuration;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace Channels.Api.Persistence;

public sealed class MongoMessagesPersistenceStore : IMessagesPersistenceStore
{
    private readonly IMongoCollection<PersistedMessageDocument> _collection;
    private readonly QueueOptions _queueOptions;
    private readonly ILogger<MongoMessagesPersistenceStore> _logger;

    public MongoMessagesPersistenceStore(
        IMongoClient mongoClient,
        IOptions<MongoOptions> mongoOptions,
        IOptions<QueueOptions> queueOptions,
        ILogger<MongoMessagesPersistenceStore> logger)
    {
        var mongo = mongoOptions.Value;
        var database = mongoClient.GetDatabase(mongo.DatabaseName);
        _collection = database.GetCollection<PersistedMessageDocument>(mongo.CollectionName);
        _queueOptions = queueOptions.Value;
        _logger = logger;
    }

    public async Task UpsertAsync(PersistedMessageDocument doc, CancellationToken ct)
    {
        var filter = Builders<PersistedMessageDocument>.Filter.Eq(x => x.Id, doc.Id);
        var options = new ReplaceOptions { IsUpsert = true };
        await _collection.ReplaceOneAsync(filter, doc, options, ct);
    }

    public async Task MarkProcessingAsync(string messageId, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var update = Builders<PersistedMessageDocument>.Update
            .Set(x => x.Status, "Processing")
            .Set(x => x.LastAttemptAt, now)
            .Inc(x => x.AttemptCount, 1);

        await _collection.UpdateOneAsync(x => x.Id == messageId, update, cancellationToken: ct);
    }

    public async Task MarkMovedToErrorAsync(string messageId, string error, CancellationToken ct)
    {
        var update = Builders<PersistedMessageDocument>.Update
            .Set(x => x.Status, "MovedToError")
            .Set(x => x.LastError, error)
            .Set(x => x.LastAttemptAt, DateTimeOffset.UtcNow);

        await _collection.UpdateOneAsync(x => x.Id == messageId, update, cancellationToken: ct);
    }

    public async Task DeleteAsync(string messageId, CancellationToken ct)
    {
        await _collection.DeleteOneAsync(x => x.Id == messageId, ct);
    }

    public async Task<IReadOnlyList<PersistedMessageDocument>> LoadUnfinishedAsync(CancellationToken ct)
    {
        var statuses = new[] { "Pending", "Processing" };
        var filter = Builders<PersistedMessageDocument>.Filter.In(x => x.Status, statuses)
            & Builders<PersistedMessageDocument>.Filter.Eq(x => x.QueueName, _queueOptions.QueueName);

        var list = await _collection.Find(filter).ToListAsync(ct);
        _logger.LogInformation("Recovered {Count} unfinished messages from MongoDB.", list.Count);
        return list;
    }

    public async Task<Dictionary<string, string>> GetStatusesAsync(IEnumerable<string> messageIds, CancellationToken ct)
    {
        var ids = messageIds.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (ids.Length == 0)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        var docs = await _collection.Find(x => ids.Contains(x.Id)).ToListAsync(ct);
        return docs.ToDictionary(x => x.Id, x => x.Status, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<bool> ExistsUnfinishedAsync(string messageId, CancellationToken ct)
    {
        var statuses = new[] { "Pending", "Processing" };
        var count = await _collection.CountDocumentsAsync(
            x => x.Id == messageId && statuses.Contains(x.Status),
            cancellationToken: ct);

        return count > 0;
    }
}
