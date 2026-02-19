namespace Channels.Api.Contracts;

public sealed record QueueEnvelope(
    string MessageId,
    string Payload,
    DateTimeOffset EnqueuedAt,
    IDictionary<string, string> Headers);

public sealed record ErrorQueueEnvelope(
    string OriginalMessageId,
    string OriginalPayload,
    DateTimeOffset FailedAt,
    string ExceptionType,
    string ExceptionMessage,
    string? StackTrace,
    IDictionary<string, string> OriginalHeaders,
    string Host);

public sealed record QueuePeekItem(
    string MessageId,
    DateTimeOffset EnqueuedAt,
    IDictionary<string, string> Headers,
    string Payload);

public sealed class QueueReceiveItem
{
    public string MessageId { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public IDictionary<string, string> Headers { get; set; } = new Dictionary<string, string>();
    public DateTimeOffset EnqueuedAt { get; set; }
    public object? NativeMessage { get; set; }
    public string QueueName { get; set; } = string.Empty;
}

public sealed record QueuePeekItemResponse(
    string MessageId,
    DateTimeOffset EnqueuedAt,
    IDictionary<string, string> Headers,
    string Payload,
    string? PersistenceStatus);

public sealed record MoveAllResult(int MovedCount);
