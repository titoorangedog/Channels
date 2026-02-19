namespace Channels.Api.Domain;

public sealed class ReportExecutionHistory
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string ReportId { get; set; } = string.Empty;
    public string User { get; set; } = string.Empty;
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public string Status { get; set; } = "Pending";
    public string? Error { get; set; }
}
