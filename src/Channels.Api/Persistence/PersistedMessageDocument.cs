using MongoDB.Bson.Serialization.Attributes;

namespace Channels.Api.Persistence;

public sealed class PersistedMessageDocument
{
    [BsonId]
    public string Id { get; set; } = string.Empty;
    public string QueueName { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public Dictionary<string, string> Headers { get; set; } = new();
    public DateTimeOffset EnqueuedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? LastAttemptAt { get; set; }
    public int AttemptCount { get; set; }
    public string Status { get; set; } = "Pending";
    public string? LastError { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
}
