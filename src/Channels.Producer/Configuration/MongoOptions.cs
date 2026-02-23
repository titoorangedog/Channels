namespace Channels.Producer.Configuration;

public sealed class MongoOptions
{
    public const int RetentionDays = 30;

    public string ConnectionString { get; set; } = "mongodb://localhost:27017";
    public string DatabaseName { get; set; } = "BackOfficeEU";
    public string CollectionName { get; set; } = "BackOfficeEU.ReportMessages";
}


