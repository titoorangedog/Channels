using ChannelsApi.Models;

namespace ChannelsApi.Services;

public interface IReportQueueService
{
    ValueTask EnqueueAsync(ReportExecutionModel model, CancellationToken cancellationToken);

    IAsyncEnumerable<ReportExecutionModel> ReadMainQueueAsync(CancellationToken cancellationToken);

    ValueTask EnqueueErrorAsync(ErrorQueueMessage errorMessage, CancellationToken cancellationToken);

    IReadOnlyCollection<ErrorQueueMessage> SnapshotErrors();

    Task<bool> RequeueErrorAsync(string messageId, CancellationToken cancellationToken);
}
