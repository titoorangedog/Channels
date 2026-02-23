namespace Channels.Producer.Configuration;

public sealed class QueueOptions
{
    public string QueueName { get; set; } = "BackOfficeEU.Reports";
    public string QueueErrorName { get; set; } = "BackOfficeEU.Reports.Error";
}


