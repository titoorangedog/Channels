using Channels.Consumer.Persistence;
using System.Threading.Channels;
using Channels.Consumer.Abstractions;
using Channels.Consumer.Configuration;
using Channels.Consumer.Contracts;
using Channels.Api.Persistence;
using Channels.Producer.Configuration;
using Channels.Producer.Pipeline;
using Channels.Consumer.Processing;
using Channels.Producer.Queue;
using Channels.Producer.Serialization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Channels.Api.Tests;

public sealed class PersistenceRecoveryTests
{
    [Fact]
    public async Task PersistedMessage_ProcessedSuccessfully_ShouldMarkCompleted_AndSkipRecovery()
    {
        var serializer = new JsonMessageSerializer();
        var queue = new InMemoryQueueClient(serializer);
        var store = new InMemoryMessagesPersistenceStore();
        var dedup = new Dedup.InMemoryDedupStore();

        var payload = serializer.Serialize(new Channels.Api.Domain.ReportExecutionModel { Id = "ok-1", ReportId = "R1", User = "u1" });
        await store.UpsertAsync(new PersistedMessageDocument
        {
            Id = "ok-1",
            QueueName = "BackOfficeEU.Reports",
            Payload = payload,
            Headers = new Dictionary<string, string>(),
            EnqueuedAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow,
            Status = "Pending",
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(30)
        }, CancellationToken.None);

        var handler = new QueueMessageHandler(
            new PassProcessor(),
            queue,
            store,
            dedup,
            Options.Create(new PipelineOptions()),
            NullLogger<QueueMessageHandler>.Instance);

        await handler.HandleAsync(new QueueReceiveItem
        {
            MessageId = "ok-1",
            Body = payload,
            Headers = new Dictionary<string, string>(),
            EnqueuedAt = DateTimeOffset.UtcNow,
            QueueName = "BackOfficeEU.Reports",
            NativeMessage = null
        }, CancellationToken.None);

        var statuses = await store.GetStatusesAsync(new[] { "ok-1" }, CancellationToken.None);
        Assert.Equal("Completed", statuses["ok-1"]);

        var unfinished = await store.LoadUnfinishedAsync(CancellationToken.None);
        Assert.DoesNotContain(unfinished, x => x.Id == "ok-1");
    }

    [Fact]
    public async Task CrashRecovery_ShouldReloadPendingAndProcess()
    {
        var serializer = new JsonMessageSerializer();
        var queue = new InMemoryQueueClient(serializer);
        var store = new InMemoryMessagesPersistenceStore();
        var dedup = new Dedup.InMemoryDedupStore();

        await store.UpsertAsync(new PersistedMessageDocument
        {
            Id = "crash-1",
            QueueName = "BackOfficeEU.Reports",
            Payload = serializer.Serialize(new Channels.Api.Domain.ReportExecutionModel { Id = "crash-1", ReportId = "R1", User = "u" }),
            Headers = new Dictionary<string, string>(),
            EnqueuedAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow,
            Status = "Pending",
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(30)
        }, CancellationToken.None);

        var channel = global::System.Threading.Channels.Channel.CreateBounded<QueueReceiveItem>(new BoundedChannelOptions(5)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = false,
            SingleWriter = true
        });

        var producer = new ProducerBackgroundService(
            queue,
            store,
            dedup,
            channel,
            Options.Create(new QueueOptions()),
            NullLogger<ProducerBackgroundService>.Instance);

        await producer.StartAsync(CancellationToken.None);
        var recovered = await channel.Reader.ReadAsync(CancellationToken.None);

        var handler = new QueueMessageHandler(
            new PassProcessor(),
            queue,
            store,
            dedup,
            Options.Create(new PipelineOptions()),
            NullLogger<QueueMessageHandler>.Instance);

        await handler.HandleAsync(recovered, CancellationToken.None);
        await producer.StopAsync(CancellationToken.None);

        var statuses = await store.GetStatusesAsync(new[] { "crash-1" }, CancellationToken.None);
        Assert.Equal("Completed", statuses["crash-1"]);
    }

    [Fact]
    public async Task DefinitiveFailure_ShouldMarkMovedToError()
    {
        var serializer = new JsonMessageSerializer();
        var queue = new InMemoryQueueClient(serializer);
        var store = new InMemoryMessagesPersistenceStore();
        var dedup = new Dedup.InMemoryDedupStore();

        var handler = new QueueMessageHandler(
            new FailProcessor(),
            queue,
            store,
            dedup,
            Options.Create(new PipelineOptions { MaxProcessingRetries = 2 }),
            NullLogger<QueueMessageHandler>.Instance);

        await store.UpsertAsync(new PersistedMessageDocument
        {
            Id = "fail-1",
            QueueName = "BackOfficeEU.Reports",
            Payload = "{}",
            Headers = new Dictionary<string, string>(),
            EnqueuedAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow,
            Status = "Pending",
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(30)
        }, CancellationToken.None);

        await handler.HandleAsync(new QueueReceiveItem
        {
            MessageId = "fail-1",
            Body = "{}",
            Headers = new Dictionary<string, string>(),
            EnqueuedAt = DateTimeOffset.UtcNow,
            QueueName = "BackOfficeEU.Reports",
            NativeMessage = null
        }, CancellationToken.None);

        var statuses = await store.GetStatusesAsync(new[] { "fail-1" }, CancellationToken.None);
        Assert.Equal("MovedToError", statuses["fail-1"]);
    }

    private sealed class PassProcessor : IMessageProcessor
    {
        public Task ProcessAsync(QueueEnvelope msg, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class FailProcessor : IMessageProcessor
    {
        public Task ProcessAsync(QueueEnvelope msg, CancellationToken ct) => throw new InvalidOperationException("fail");
    }
}


