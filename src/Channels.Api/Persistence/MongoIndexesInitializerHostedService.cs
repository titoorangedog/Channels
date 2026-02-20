using Channels.Consumer.Persistence;
using Channels.Api.Configuration;
using Channels.Consumer.Configuration;
using Microsoft.Extensions.Options;
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

        var ttlIndex = new CreateIndexModel<PersistedMessageDocument>(
            Builders<PersistedMessageDocument>.IndexKeys.Ascending(x => x.ExpiresAt),
            new CreateIndexOptions { Name = "ttl_expires_at", ExpireAfter = TimeSpan.Zero });

        await collection.Indexes.CreateOneAsync(ttlIndex, cancellationToken: cancellationToken);
        _logger.LogInformation("MongoDB TTL index initialized on {Collection}.", options.CollectionName);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}


