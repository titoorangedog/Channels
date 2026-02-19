using Azure.Messaging.ServiceBus;
using Channels.Api.Abstractions;
using Channels.Api.Configuration;
using Channels.Api.Contracts;
using Microsoft.Extensions.Options;

namespace Channels.Api.Queue;

public sealed class AzureServiceBusQueueClient : IQueueClient, IAsyncDisposable
{
    private readonly ServiceBusClient _client;
    private readonly ServiceBusSender _mainSender;
    private readonly ServiceBusSender _errorSender;
    private readonly ServiceBusReceiver _mainReceiver;
    private readonly ServiceBusReceiver _errorReceiver;
    private readonly QueueOptions _queueOptions;
    private readonly PipelineOptions _pipelineOptions;
    private readonly IMessageSerializer _serializer;

    public AzureServiceBusQueueClient(
        IOptions<QueueOptions> queueOptions,
        IOptions<PipelineOptions> pipelineOptions,
        IMessageSerializer serializer,
        ILogger<AzureServiceBusQueueClient> logger)
    {
        _queueOptions = queueOptions.Value;
        _pipelineOptions = pipelineOptions.Value;
        _serializer = serializer;

        _client = new ServiceBusClient(_queueOptions.ConnectionString);
        _mainSender = _client.CreateSender(_queueOptions.QueueName);
        _errorSender = _client.CreateSender(_queueOptions.QueueErrorName);

        _mainReceiver = _client.CreateReceiver(_queueOptions.QueueName, new ServiceBusReceiverOptions
        {
            ReceiveMode = ServiceBusReceiveMode.PeekLock
        });
        _errorReceiver = _client.CreateReceiver(_queueOptions.QueueErrorName, new ServiceBusReceiverOptions
        {
            ReceiveMode = ServiceBusReceiveMode.PeekLock
        });
    }

    public async Task EnqueueMainAsync(QueueEnvelope envelope, CancellationToken ct)
    {
        var message = new ServiceBusMessage(envelope.Payload)
        {
            MessageId = envelope.MessageId
        };

        foreach (var (key, value) in _serializer.NormalizeHeaders(envelope.Headers))
        {
            message.ApplicationProperties[key] = value;
        }

        await _mainSender.SendMessageAsync(message, ct);
    }

    public async Task EnqueueErrorAsync(ErrorQueueEnvelope envelope, CancellationToken ct)
    {
        var payload = _serializer.Serialize(envelope);
        var message = new ServiceBusMessage(payload)
        {
            MessageId = envelope.OriginalMessageId
        };
        message.ApplicationProperties["ErrorEnvelope"] = "true";

        await _errorSender.SendMessageAsync(message, ct);
    }

    public async Task<IReadOnlyList<QueuePeekItem>> PeekMainAsync(int max, CancellationToken ct)
    {
        var messages = await _mainReceiver.PeekMessagesAsync(max, cancellationToken: ct);
        return messages.Select(MapPeek).ToList();
    }

    public async Task<IReadOnlyList<QueuePeekItem>> PeekErrorAsync(int max, CancellationToken ct)
    {
        var messages = await _errorReceiver.PeekMessagesAsync(max, cancellationToken: ct);
        return messages.Select(MapPeek).ToList();
    }

    public async Task<QueueReceiveItem?> ReceiveMainAsync(CancellationToken ct)
    {
        var message = await _mainReceiver.ReceiveMessageAsync(
            TimeSpan.FromMilliseconds(_pipelineOptions.ReceiveWaitTimeMs),
            ct);

        return message is null ? null : MapReceive(message, _queueOptions.QueueName);
    }

    public async Task<QueueReceiveItem?> ReceiveErrorAsync(CancellationToken ct)
    {
        var message = await _errorReceiver.ReceiveMessageAsync(
            TimeSpan.FromMilliseconds(_pipelineOptions.ReceiveWaitTimeMs),
            ct);

        return message is null ? null : MapReceive(message, _queueOptions.QueueErrorName);
    }

    public async Task CompleteAsync(QueueReceiveItem item, CancellationToken ct)
    {
        if (item.NativeMessage is not ServiceBusReceivedMessage sbMessage)
        {
            return;
        }

        var receiver = item.QueueName == _queueOptions.QueueErrorName ? _errorReceiver : _mainReceiver;
        await receiver.CompleteMessageAsync(sbMessage, ct);
    }

    public async Task AbandonAsync(QueueReceiveItem item, CancellationToken ct)
    {
        if (item.NativeMessage is not ServiceBusReceivedMessage sbMessage)
        {
            return;
        }

        var receiver = item.QueueName == _queueOptions.QueueErrorName ? _errorReceiver : _mainReceiver;
        await receiver.AbandonMessageAsync(sbMessage, cancellationToken: ct);
    }

    public async ValueTask DisposeAsync()
    {
        await _mainReceiver.DisposeAsync();
        await _errorReceiver.DisposeAsync();
        await _mainSender.DisposeAsync();
        await _errorSender.DisposeAsync();
        await _client.DisposeAsync();
    }

    private static QueuePeekItem MapPeek(ServiceBusReceivedMessage message)
    {
        return new QueuePeekItem(
            message.MessageId,
            message.EnqueuedTime,
            ToHeaderDictionary(message.ApplicationProperties),
            message.Body.ToString());
    }

    private static QueueReceiveItem MapReceive(ServiceBusReceivedMessage message, string queueName)
    {
        return new QueueReceiveItem
        {
            MessageId = message.MessageId,
            Body = message.Body.ToString(),
            Headers = ToHeaderDictionary(message.ApplicationProperties),
            EnqueuedAt = message.EnqueuedTime,
            NativeMessage = message,
            QueueName = queueName
        };
    }

    private static IDictionary<string, string> ToHeaderDictionary(IReadOnlyDictionary<string, object> applicationProperties)
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in applicationProperties)
        {
            headers[key] = value?.ToString() ?? string.Empty;
        }

        return headers;
    }
}
