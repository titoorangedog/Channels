using Channels.Consumer.Persistence;
using Channels.Consumer.Abstractions;
using Channels.Consumer.Configuration;
using Channels.Consumer.Contracts;
using Channels.Api.Persistence;
using Channels.Consumer.Processing;
using Channels.Api.Queue;
using Channels.Api.Serialization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Channels.Api.Tests;

public sealed class ProcessingPipelineTests
{
    [Fact]
    public async Task ConsumerFailure_ShouldMoveMessageToError_AndCompleteMain()
    {
        var serializer = new JsonMessageSerializer();
        var queueClient = new InMemoryQueueClient(serializer);
        var store = new InMemoryMessagesPersistenceStore();
        var dedup = new Dedup.InMemoryDedupStore();
        var pipelineOptions = Options.Create(new PipelineOptions { MaxProcessingRetries = 3 });

        var handler = new QueueMessageHandler(
            new AlwaysFailProcessor(),
            queueClient,
            store,
            dedup,
            pipelineOptions,
            NullLogger<QueueMessageHandler>.Instance);

        var payload = serializer.Serialize(new Channels.Api.Domain.ReportExecutionModel { Id = "bad-1", ReportId = "", User = "" });
        await queueClient.EnqueueMainAsync(new QueueEnvelope("bad-1", payload, DateTimeOffset.UtcNow, new Dictionary<string, string>()), CancellationToken.None);
        await store.UpsertAsync(new PersistedMessageDocument
        {
            Id = "bad-1",
            QueueName = "BackOfficeEU.Reports",
            Payload = payload,
            Headers = new Dictionary<string, string>(),
            EnqueuedAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow,
            Status = "Pending",
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(30)
        }, CancellationToken.None);

        var received = await queueClient.ReceiveMainAsync(CancellationToken.None);
        Assert.NotNull(received);

        await handler.HandleAsync(received!, CancellationToken.None);

        var main = await queueClient.PeekMainAsync(10, CancellationToken.None);
        var error = await queueClient.PeekErrorAsync(10, CancellationToken.None);
        var statuses = await store.GetStatusesAsync(new[] { "bad-1" }, CancellationToken.None);

        Assert.Empty(main);
        Assert.Single(error);
        Assert.Equal("MovedToError", statuses["bad-1"]);
    }

    [Fact]
    public async Task RetryCount_ShouldBeRespected()
    {
        var serializer = new JsonMessageSerializer();
        var queueClient = new InMemoryQueueClient(serializer);
        var store = new InMemoryMessagesPersistenceStore();
        var dedup = new Dedup.InMemoryDedupStore();
        var pipelineOptions = Options.Create(new PipelineOptions { MaxProcessingRetries = 3 });

        var flakyProcessor = new FlakyProcessor(2);
        var handler = new QueueMessageHandler(
            flakyProcessor,
            queueClient,
            store,
            dedup,
            pipelineOptions,
            NullLogger<QueueMessageHandler>.Instance);

        var model = new Channels.Api.Domain.ReportExecutionModel { Id = "retry-1", ReportId = "RPT", User = "u" };
        var payload = serializer.Serialize(model);
        await store.UpsertAsync(new PersistedMessageDocument
        {
            Id = "retry-1",
            QueueName = "BackOfficeEU.Reports",
            Payload = payload,
            Headers = new Dictionary<string, string>(),
            EnqueuedAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow,
            Status = "Pending",
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(30)
        }, CancellationToken.None);

        await handler.HandleAsync(new QueueReceiveItem
        {
            MessageId = "retry-1",
            Body = payload,
            Headers = new Dictionary<string, string>(),
            EnqueuedAt = DateTimeOffset.UtcNow,
            QueueName = "BackOfficeEU.Reports",
            NativeMessage = null
        }, CancellationToken.None);

        Assert.Equal(3, flakyProcessor.Attempts);
        var statuses = await store.GetStatusesAsync(new[] { "retry-1" }, CancellationToken.None);
        Assert.False(statuses.ContainsKey("retry-1"));
    }

    private sealed class AlwaysFailProcessor : IMessageProcessor
    {
        public Task ProcessAsync(QueueEnvelope msg, CancellationToken ct) => throw new InvalidOperationException("boom");
    }

    private sealed class FlakyProcessor : IMessageProcessor
    {
        private readonly int _failuresBeforeSuccess;
        public int Attempts { get; private set; }

        public FlakyProcessor(int failuresBeforeSuccess)
        {
            _failuresBeforeSuccess = failuresBeforeSuccess;
        }

        public Task ProcessAsync(QueueEnvelope msg, CancellationToken ct)
        {
            Attempts++;
            if (Attempts <= _failuresBeforeSuccess)
            {
                throw new InvalidOperationException("transient");
            }

            return Task.CompletedTask;
        }
    }
}


