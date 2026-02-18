using QueueService.Models;

namespace QueueService.Services;

public interface IReportQueueService
{
    ValueTask EnqueueAsync(ReportExecutionModel model, CancellationToken cancellationToken);
    ValueTask<ReportExecutionModel?> TryDequeueAsync(CancellationToken cancellationToken);
    ValueTask FailAsync(FailMessageRequest request, CancellationToken cancellationToken);
    IReadOnlyCollection<ErrorQueueMessage> SnapshotErrors();
    Task<bool> RequeueErrorAsync(string messageId, CancellationToken cancellationToken);
}
