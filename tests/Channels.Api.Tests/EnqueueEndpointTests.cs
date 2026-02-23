using Channels.Consumer.Configuration;
using Channels.Api.Domain;
using Channels.Api.Endpoints;
using Channels.Api.Persistence;
using Channels.Producer.Configuration;
using Channels.Producer.Queue;
using Channels.Producer.Serialization;
using Microsoft.Extensions.Options;

namespace Channels.Api.Tests;

public sealed class EnqueueEndpointTests
{
    [Fact]
    public async Task EnqueueEndpoint_ShouldProduceMessageInMainQueue()
    {
        var serializer = new JsonMessageSerializer();
        var queueClient = new InMemoryQueueClient(serializer);
        var store = new InMemoryMessagesPersistenceStore();
        var queueOptions = Options.Create(new QueueOptions());

        var model = new ReportExecutionModel
        {
            Id = "msg-1",
            ReportId = "RPT-1",
            User = "alice"
        };

        var result = await QueueEndpoints.EnqueueReportAsync(
            model,
            queueClient,
            store,
            serializer,
            queueOptions,
            CancellationToken.None);

        var peek = await queueClient.PeekMainAsync(10, CancellationToken.None);
        Assert.Single(peek);
        Assert.Equal("msg-1", peek[0].MessageId);
        Assert.NotNull(result);
    }

    [Fact]
    public async Task PeekMainMessages_ShouldIncludePersistedPending_WhenQueueIsEmpty()
    {
        var serializer = new JsonMessageSerializer();
        var queueClient = new InMemoryQueueClient(serializer);
        var store = new InMemoryMessagesPersistenceStore();

        await store.UpsertAsync(new Channels.Consumer.Persistence.PersistedMessageDocument
        {
            Id = "pending-1",
            QueueName = "BackOfficeEU.Reports",
            Payload = "{\"id\":\"pending-1\"}",
            Headers = new Dictionary<string, string>(),
            EnqueuedAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow,
            Status = "Pending",
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(30)
        }, CancellationToken.None);

        var result = await QueueEndpoints.PeekMainMessagesAsync(
            100,
            queueClient,
            store,
            Options.Create(new PipelineOptions()),
            CancellationToken.None);

        Assert.Single(result.Value);
        Assert.Equal("pending-1", result.Value[0].MessageId);
        Assert.Equal("Pending", result.Value[0].PersistenceStatus);
    }
}


