using Channels.Consumer.Contracts;

namespace Channels.Consumer.Abstractions;

public interface IMessageProcessor
{
    Task ProcessAsync(QueueEnvelope msg, CancellationToken ct);
}


