namespace QueueService.Models;

public sealed record ReportExecutionModel(
    string Id,
    string ReportId,
    string ClassName,
    string User,
    int ExecutionCount = 0,
    Dictionary<string, string>? Data = null
);
