using Channels.Consumer.Persistence;
using Channels.Producer.Configuration;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Channels.Api.Persistence;

public sealed class MongoIndexesInitializerHostedService : IHostedService
{
    private readonly IMongoClient _mongoClient;
    private readonly IOptions<MongoOptions> _mongoOptions;
    private readonly ILogger<MongoIndexesInitializerHostedService> _logger;

    public MongoIndexesInitializerHostedService(
        IMongoClient mongoClient,
        IOptions<MongoOptions> mongoOptions,
        ILogger<MongoIndexesInitializerHostedService> logger)
    {
        _mongoClient = mongoClient;
        _mongoOptions = mongoOptions;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var options = _mongoOptions.Value;
        if (string.Equals(options.ConnectionString, "InMemory", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var database = _mongoClient.GetDatabase(options.DatabaseName);
        var collection = database.GetCollection<PersistedMessageDocument>(options.CollectionName);

        var indexes = await (await collection.Indexes.ListAsync(cancellationToken)).ToListAsync(cancellationToken);
        var ttlIndex = indexes.FirstOrDefault(x => x.TryGetValue("name", out var name) && name == "ttl_expires_at");
        var ttlIndexIsValid = ttlIndex is not null
            && ttlIndex.TryGetValue("expireAfterSeconds", out var expireAfterSeconds)
            && expireAfterSeconds.ToInt32() == 0
            && ttlIndex.TryGetValue("key", out var keyDoc)
            && keyDoc.AsBsonDocument.TryGetValue(nameof(PersistedMessageDocument.ExpiresAt), out var expiresKey)
            && expiresKey.ToInt32() == 1;

        if (ttlIndex is not null && !ttlIndexIsValid)
        {
            await collection.Indexes.DropOneAsync("ttl_expires_at", cancellationToken);
        }

        var ttlIndexModel = new CreateIndexModel<PersistedMessageDocument>(
            Builders<PersistedMessageDocument>.IndexKeys.Ascending(x => x.ExpiresAt),
            new CreateIndexOptions { Name = "ttl_expires_at", ExpireAfter = TimeSpan.Zero });

        if (!ttlIndexIsValid)
        {
            await collection.Indexes.CreateOneAsync(ttlIndexModel, cancellationToken: cancellationToken);
        }

        _logger.LogInformation(
            "MongoDB TTL retention verified on {Collection}: documents expire after {RetentionDays} days via ExpiresAt.",
            options.CollectionName,
            MongoOptions.RetentionDays);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}


