using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.Extensions.Options;
using QueueService.Configuration;
using QueueService.Models;

namespace QueueService.Services;

public sealed class ReportQueueService : IReportQueueService
{
    private readonly Channel<ReportExecutionModel> _mainChannel;
    private readonly ConcurrentDictionary<string, ErrorQueueMessage> _errors = new(StringComparer.OrdinalIgnoreCase);
    private readonly ILogger<ReportQueueService> _logger;

    public ReportQueueService(IOptions<QueueOptions> options, ILogger<ReportQueueService> logger)
    {
        _logger = logger;

        var cfg = options.Value;
        if (string.IsNullOrWhiteSpace(cfg.QueueName))
        {
            throw new InvalidOperationException("QueueName non configurata.");
        }

        if (string.IsNullOrWhiteSpace(cfg.QueueErrorName))
        {
            throw new InvalidOperationException("QueueErrorName non configurata.");
        }

        _mainChannel = Channel.CreateBounded<ReportExecutionModel>(new BoundedChannelOptions(cfg.Capacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        });
    }

    public async ValueTask EnqueueAsync(ReportExecutionModel model, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(model);
        var payload = model with { ExecutionCount = model.ExecutionCount + 1 };
        await _mainChannel.Writer.WriteAsync(payload, cancellationToken);
        _logger.LogInformation("Messaggio {MessageId} accodato su queue principale", payload.Id);
    }

    public async ValueTask<ReportExecutionModel?> TryDequeueAsync(CancellationToken cancellationToken)
    {
        if (await _mainChannel.Reader.WaitToReadAsync(cancellationToken)
            && _mainChannel.Reader.TryRead(out var item))
        {
            return item;
        }

        return null;
    }

    public ValueTask FailAsync(FailMessageRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var error = new ErrorQueueMessage(
            request.Payload,
            request.ErrorMessage,
            DateTimeOffset.UtcNow,
            request.ExceptionType);

        _errors[request.Payload.Id] = error;
        _logger.LogWarning("Messaggio {MessageId} spostato nella queue errori: {Error}", request.Payload.Id, request.ErrorMessage);
        return ValueTask.CompletedTask;
    }

    public IReadOnlyCollection<ErrorQueueMessage> SnapshotErrors() => _errors.Values.OrderByDescending(x => x.FailedAt).ToArray();

    public async Task<bool> RequeueErrorAsync(string messageId, CancellationToken cancellationToken)
    {
        if (!_errors.TryRemove(messageId, out var errorMessage))
        {
            return false;
        }

        await EnqueueAsync(errorMessage.Payload, cancellationToken);
        return true;
    }
}
