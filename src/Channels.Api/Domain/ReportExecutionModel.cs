namespace Channels.Api.Domain;

public sealed class ReportExecutionModel : ReportExecutionModelBase
{
    public bool Priority { get; set; }
    public string? CorrelationId { get; set; }
}

