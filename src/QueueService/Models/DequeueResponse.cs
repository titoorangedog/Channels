namespace QueueService.Models;

public sealed record DequeueResponse(bool HasMessage, ReportExecutionModel? Message);

public sealed record FailMessageRequest(
    ReportExecutionModel Payload,
    string ErrorMessage,
    string? ExceptionType = null
);
