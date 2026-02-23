using System;
using System.Collections.Generic;

namespace QueueService
{
    public sealed record ReportExecutionModel(
        string Id,
        string ClassName,
        string User,
        string ReportId,
        IDictionary<string, string>? Data,
        int ExecutionCount = 0);

    public sealed record ErrorQueueMessage(
        ReportExecutionModel Payload,
        string ErrorMessage,
        DateTimeOffset FailedAt,
        string? ExceptionType = null);
}
