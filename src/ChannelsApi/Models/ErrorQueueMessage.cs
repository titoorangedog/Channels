namespace ChannelsApi.Models;

public sealed record ErrorQueueMessage(
    ReportExecutionModel Payload,
    string ErrorMessage,
    DateTimeOffset FailedAt,
    string? ExceptionType = null
);
