namespace ReportConsumer.Models;

public sealed record ReportExecutionModel(
    string Id,
    string ReportId,
    string ClassName,
    string User,
    int ExecutionCount = 0,
    Dictionary<string, string>? Data = null
);

public sealed record DequeueResponse(bool HasMessage, ReportExecutionModel? Message);

public sealed record FailMessageRequest(
    ReportExecutionModel Payload,
    string ErrorMessage,
    string? ExceptionType = null
);
