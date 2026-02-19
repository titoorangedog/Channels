namespace Channels.Api.Domain;

public sealed class ReportExecutionResult
{
    public string MessageId { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? OutputUri { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTimeOffset CompletedAt { get; set; } = DateTimeOffset.UtcNow;
}
