using Channels.Api.Configuration;
using Channels.Api.Domain;
using Channels.Api.Endpoints;
using Channels.Api.Persistence;
using Channels.Api.Queue;
using Channels.Api.Serialization;
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
        var mongoOptions = Options.Create(new MongoOptions { ConnectionString = "InMemory" });

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
            mongoOptions,
            CancellationToken.None);

        var peek = await queueClient.PeekMainAsync(10, CancellationToken.None);
        Assert.Single(peek);
        Assert.Equal("msg-1", peek[0].MessageId);
        Assert.NotNull(result);
    }
}
