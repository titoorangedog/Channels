namespace ReportConsumer.Configuration;

public sealed class ConsumerOptions
{
    public const string SectionName = "ConsumerSettings";

    public string QueueServiceBaseUrl { get; init; } = "http://localhost:5000";

    public int EmptyQueueDelayMs { get; init; } = 300;
}
