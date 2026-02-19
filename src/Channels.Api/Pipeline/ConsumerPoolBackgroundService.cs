using System.Threading.Channels;
using Channels.Api.Configuration;
using Channels.Api.Contracts;
using Channels.Api.Processing;
using Microsoft.Extensions.Options;

namespace Channels.Api.Pipeline;

public sealed class ConsumerPoolBackgroundService : IHostedService
{
    private readonly Channel<QueueReceiveItem> _channel;
    private readonly QueueMessageHandler _handler;
    private readonly PipelineOptions _options;
    private readonly ILogger<ConsumerPoolBackgroundService> _logger;

    private Task? _runTask;

    public ConsumerPoolBackgroundService(
        Channel<QueueReceiveItem> channel,
        QueueMessageHandler handler,
        IOptions<PipelineOptions> options,
        ILogger<ConsumerPoolBackgroundService> logger)
    {
        _channel = channel;
        _handler = handler;
        _options = options.Value;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _runTask = Task.Run(() => RunConsumersAsync(cancellationToken), CancellationToken.None);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_runTask is null)
        {
            return;
        }

        var drainTimeout = TimeSpan.FromSeconds(Math.Max(1, _options.ShutdownDrainTimeoutSeconds));
        var completed = await Task.WhenAny(_runTask, Task.Delay(drainTimeout, cancellationToken));

        if (completed != _runTask)
        {
            _logger.LogWarning("Consumer drain timeout reached after {DrainTimeout}.", drainTimeout);
        }
    }

    private async Task RunConsumersAsync(CancellationToken ct)
    {
        var consumerCount = Math.Clamp(_options.ConsumerCount, 1, 64);
        var tasks = Enumerable.Range(0, consumerCount)
            .Select(index => ConsumeLoopAsync(index + 1, ct))
            .ToArray();

        await Task.WhenAll(tasks);
    }

    private async Task ConsumeLoopAsync(int workerId, CancellationToken ct)
    {
        await foreach (var item in _channel.Reader.ReadAllAsync(ct))
        {
            _logger.LogDebug("Worker {WorkerId} handling message {MessageId}.", workerId, item.MessageId);
            await _handler.HandleAsync(item, ct);
        }
    }
}
