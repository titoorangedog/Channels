using ChannelsApi.Models;

namespace ChannelsApi.Services;

public interface IReportRunnerService
{
    Task ExecuteReportAsync(ReportExecutionModel model, CancellationToken cancellationToken);
}
