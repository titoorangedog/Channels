using System.Collections.Concurrent;
using System.Threading.Channels;
using ChannelsApi.Configuration;
using ChannelsApi.Models;
using Microsoft.Extensions.Options;

namespace ChannelsApi.Services;

public sealed class ReportQueueService : IReportQueueService
{
    private readonly Channel<ReportExecutionModel> _mainChannel;
    private readonly Channel<ErrorQueueMessage> _errorChannel;
    private readonly ConcurrentDictionary<string, ErrorQueueMessage> _errors = new(StringComparer.OrdinalIgnoreCase);
    private readonly ILogger<ReportQueueService> _logger;

    public ReportQueueService(IOptions<QueueOptions> options, ILogger<ReportQueueService> logger)
    {
        _logger = logger;

        var queueOptions = options.Value;
        if (string.IsNullOrWhiteSpace(queueOptions.QueueName))
        {
            throw new InvalidOperationException("QueueName non configurata.");
        }

        if (string.IsNullOrWhiteSpace(queueOptions.QueueErrorName))
        {
            throw new InvalidOperationException("QueueErrorName non configurata.");
        }

        var boundedOptions = new BoundedChannelOptions(queueOptions.Capacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        };

        _mainChannel = Channel.CreateBounded<ReportExecutionModel>(boundedOptions);
        _errorChannel = Channel.CreateUnbounded<ErrorQueueMessage>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });
    }

    public async ValueTask EnqueueAsync(ReportExecutionModel model, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(model);

        var payload = model with { ExecutionCount = model.ExecutionCount + 1 };
        await _mainChannel.Writer.WriteAsync(payload, cancellationToken);

        _logger.LogInformation("Messaggio {MessageId} accodato nella queue principale", payload.Id);
    }

    public IAsyncEnumerable<ReportExecutionModel> ReadMainQueueAsync(CancellationToken cancellationToken)
        => _mainChannel.Reader.ReadAllAsync(cancellationToken);

    public async ValueTask EnqueueErrorAsync(ErrorQueueMessage errorMessage, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(errorMessage);

        await _errorChannel.Writer.WriteAsync(errorMessage, cancellationToken);
        _errors[errorMessage.Payload.Id] = errorMessage;

        _logger.LogWarning(
            "Messaggio {MessageId} spostato nella queue errori: {Error}",
            errorMessage.Payload.Id,
            errorMessage.ErrorMessage);
    }

    public IReadOnlyCollection<ErrorQueueMessage> SnapshotErrors()
        => _errors.Values.OrderByDescending(x => x.FailedAt).ToArray();

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
