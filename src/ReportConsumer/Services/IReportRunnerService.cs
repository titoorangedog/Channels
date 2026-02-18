using ReportConsumer.Models;

namespace ReportConsumer.Services;

public interface IReportRunnerService
{
    Task ExecuteReportAsync(ReportExecutionModel model, CancellationToken cancellationToken);
}
