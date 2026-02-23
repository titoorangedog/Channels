namespace Channels.Api.Domain;

public class ReportExecutionModelBase : InfoExecutionModelBase
{
    public string ReportId { get; set; } = string.Empty;
    public string? TenantId { get; set; }
    public List<QueryParameter> Parameters { get; set; } = new();
}

