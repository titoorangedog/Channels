using Channels.Consumer.Abstractions;
using Channels.Consumer.Contracts;
using Channels.Api.Domain;

namespace Channels.Api.Processing;

public sealed class DummyMessageProcessor : IMessageProcessor
{
    private readonly IMessageSerializer _serializer;
    private readonly ILogger<DummyMessageProcessor> _logger;

    public DummyMessageProcessor(IMessageSerializer serializer, ILogger<DummyMessageProcessor> logger)
    {
        _serializer = serializer;
        _logger = logger;
    }

    public async Task ProcessAsync(QueueEnvelope msg, CancellationToken ct)
    {
        var model = _serializer.Deserialize<ReportExecutionModel>(msg.Payload);

        if (string.IsNullOrWhiteSpace(model.ReportId) || string.IsNullOrWhiteSpace(model.User))
        {
            throw new InvalidOperationException("ReportId and User are required.");
        }

        await Task.Delay(200, ct);
        _logger.LogInformation("Processed message {MessageId} for report {ReportId} and user {User}.", msg.MessageId, model.ReportId, model.User);
    }
}


