namespace ChannelsApi.Configuration;

public sealed class QueueOptions
{
    public const string SectionName = "QueueSettings";

    public string QueueName { get; init; } = "BackOfficeEU.Reports";

    public string QueueErrorName { get; init; } = "BackOfficeEU.Reports.Error";

    public int Capacity { get; init; } = 1_000;
}
