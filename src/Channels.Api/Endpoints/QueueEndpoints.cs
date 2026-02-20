using Channels.Consumer.Persistence;
using Channels.Consumer.Abstractions;
using Channels.Consumer.Configuration;
using Channels.Consumer.Contracts;
using Channels.Api.Domain;
using Channels.Api.Persistence;
using Channels.Api.Services;
using Channels.Producer.Configuration;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;

namespace Channels.Api.Endpoints;

public static class QueueEndpoints
{
    public static void MapQueueEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/reports/enqueue", EnqueueReportAsync);
        app.MapGet("/api/queues/main/messages", PeekMainMessagesAsync);
        app.MapGet("/api/queues/error/messages", PeekErrorMessagesAsync);
        app.MapPost("/api/queues/error/move/{messageId}", MoveErrorByIdAsync);
        app.MapPost("/api/queues/error/move-all", MoveAllErrorMessagesAsync);
    }

    public static async Task<Results<Accepted<object>, BadRequest<string>, ProblemHttpResult>> EnqueueReportAsync(
        ReportExecutionModel model,
        IQueueClient queueClient,
        IMessagesPersistenceStore store,
        IMessageSerializer serializer,
        IOptions<QueueOptions> queueOptions,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(model.ReportId) || string.IsNullOrWhiteSpace(model.User))
        {
            return TypedResults.BadRequest("ReportId and User are required.");
        }

        var messageId = string.IsNullOrWhiteSpace(model.Id) ? Guid.NewGuid().ToString("N") : model.Id;
        var payload = serializer.Serialize(model);
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["MessageType"] = nameof(ReportExecutionModel),
            ["ContentType"] = "application/json"
        };

        var now = DateTimeOffset.UtcNow;
        var persisted = new PersistedMessageDocument
        {
            Id = messageId,
            QueueName = queueOptions.Value.QueueName,
            Payload = payload,
            Headers = headers,
            EnqueuedAt = now,
            CreatedAt = now,
            Status = "Pending",
            ExpiresAt = now.AddDays(MongoOptions.RetentionDays)
        };

        await store.UpsertAsync(persisted, ct);

        try
        {
            var envelope = new QueueEnvelope(messageId, payload, now, headers);
            await queueClient.EnqueueMainAsync(envelope, ct);
        }
        catch (Exception ex)
        {
            await store.MarkMovedToErrorAsync(messageId, $"Enqueue failed: {ex.Message}", ct);
            return TypedResults.Problem($"Failed to enqueue message: {ex.Message}");
        }

        return TypedResults.Accepted($"/api/queues/main/messages?messageId={messageId}", (object)new { MessageId = messageId });
    }

    public static async Task<Ok<IReadOnlyList<QueuePeekItemResponse>>> PeekMainMessagesAsync(
        int? max,
        IQueueClient queueClient,
        IMessagesPersistenceStore store,
        IOptions<PipelineOptions> pipelineOptions,
        CancellationToken ct)
    {
        var limit = ResolveMax(max, pipelineOptions.Value.PeekMaxDefault);
        var peeked = await queueClient.PeekMainAsync(limit, ct);
        var statuses = await store.GetStatusesAsync(peeked.Select(x => x.MessageId), ct);
        var merged = new List<QueuePeekItemResponse>(limit * 2);
        var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in peeked)
        {
            merged.Add(new QueuePeekItemResponse(
                item.MessageId,
                item.EnqueuedAt,
                item.Headers,
                item.Payload,
                statuses.GetValueOrDefault(item.MessageId)));

            seenIds.Add(item.MessageId);
        }

        if (merged.Count < limit)
        {
            var unfinished = await store.LoadUnfinishedAsync(ct);
            foreach (var doc in unfinished.OrderBy(x => x.EnqueuedAt))
            {
                if (!seenIds.Add(doc.Id))
                {
                    continue;
                }

                merged.Add(new QueuePeekItemResponse(
                    doc.Id,
                    doc.EnqueuedAt,
                    doc.Headers,
                    doc.Payload,
                    doc.Status));
            }
        }

        var response = merged
            .OrderBy(x => x.EnqueuedAt)
            .ThenBy(x => x.MessageId, StringComparer.OrdinalIgnoreCase)
            .Take(limit)
            .ToList();

        return TypedResults.Ok<IReadOnlyList<QueuePeekItemResponse>>(response);
    }

    public static async Task<Ok<IReadOnlyList<QueuePeekItemResponse>>> PeekErrorMessagesAsync(
        int? max,
        IQueueClient queueClient,
        IMessagesPersistenceStore store,
        IOptions<PipelineOptions> pipelineOptions,
        IMessageSerializer serializer,
        CancellationToken ct)
    {
        var limit = ResolveMax(max, pipelineOptions.Value.PeekMaxDefault);
        var peeked = await queueClient.PeekErrorAsync(limit, ct);

        var ids = peeked.Select(x =>
        {
            try
            {
                var error = serializer.Deserialize<ErrorQueueEnvelope>(x.Payload);
                return error.OriginalMessageId;
            }
            catch
            {
                return x.MessageId;
            }
        }).ToArray();

        var statuses = await store.GetStatusesAsync(ids, ct);

        var response = peeked
            .Select(x =>
            {
                var originalId = x.MessageId;
                try
                {
                    var error = serializer.Deserialize<ErrorQueueEnvelope>(x.Payload);
                    originalId = error.OriginalMessageId;
                }
                catch
                {
                }

                return new QueuePeekItemResponse(
                    x.MessageId,
                    x.EnqueuedAt,
                    x.Headers,
                    x.Payload,
                    statuses.GetValueOrDefault(originalId));
            })
            .ToList();

        return TypedResults.Ok<IReadOnlyList<QueuePeekItemResponse>>(response);
    }

    public static async Task<Results<Ok, NotFound>> MoveErrorByIdAsync(
        string messageId,
        QueueMoveService queueMoveService,
        CancellationToken ct)
    {
        var moved = await queueMoveService.MoveByIdAsync(messageId, ct);
        return moved ? TypedResults.Ok() : TypedResults.NotFound();
    }

    public static async Task<Ok<MoveAllResult>> MoveAllErrorMessagesAsync(
        QueueMoveService queueMoveService,
        CancellationToken ct)
    {
        var movedCount = await queueMoveService.MoveAllAsync(ct);
        return TypedResults.Ok(new MoveAllResult(movedCount));
    }

    private static int ResolveMax(int? requested, int defaultMax)
    {
        var value = requested.GetValueOrDefault(defaultMax);
        return Math.Clamp(value, 1, 1000);
    }
}


