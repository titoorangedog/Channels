namespace Channels.Api.Configuration;

public sealed class QueueOptions
{
    public string Provider { get; set; } = "AzureServiceBus";
    public string ConnectionString { get; set; } = string.Empty;
    public string QueueName { get; set; } = "BackOfficeEU.Reports";
    public string QueueErrorName { get; set; } = "BackOfficeEU.Reports.Error";
}
