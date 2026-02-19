using Channels.Api.Contracts;

namespace Channels.Api.Abstractions;

public interface IMessageProcessor
{
    Task ProcessAsync(QueueEnvelope msg, CancellationToken ct);
}
