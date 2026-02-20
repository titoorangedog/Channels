using Channels.Api.Configuration;
using Channels.Consumer.Configuration;
using Channels.Consumer.Contracts;
using Channels.Api.Persistence;
using Channels.Api.Queue;
using Channels.Api.Serialization;
using Channels.Api.Services;
using Microsoft.Extensions.Options;

namespace Channels.Api.Tests;

public sealed class QueueMoveServiceTests
{
    [Fact]
    public async Task MoveAll_ShouldMoveAllMessagesFromErrorToMain()
    {
        var serializer = new JsonMessageSerializer();
        var queueClient = new InMemoryQueueClient(serializer);
        var store = new InMemoryMessagesPersistenceStore();
        var service = new QueueMoveService(
            queueClient,
            serializer,
            store,
            Options.Create(new QueueOptions()),
            Options.Create(new PipelineOptions()),
            Options.Create(new MongoOptions { ConnectionString = "InMemory" }));

        for (var i = 0; i < 3; i++)
        {
            await queueClient.EnqueueErrorAsync(new ErrorQueueEnvelope(
                $"err-{i}",
                "{}",
                DateTimeOffset.UtcNow,
                "System.Exception",
                "x",
                null,
                new Dictionary<string, string>(),
                "host"), CancellationToken.None);
        }

        var moved = await service.MoveAllAsync(CancellationToken.None);
        var main = await queueClient.PeekMainAsync(10, CancellationToken.None);
        var error = await queueClient.PeekErrorAsync(10, CancellationToken.None);

        Assert.Equal(3, moved);
        Assert.Equal(3, main.Count);
        Assert.Empty(error);
    }
}


