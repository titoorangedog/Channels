using Channels.Api.Configuration;
namespace Channels.Api.Configuration;

public sealed class MongoOptions
{
    public string ConnectionString { get; set; } = "mongodb://localhost:27017";
    public string DatabaseName { get; set; } = "BackOfficeEU";
    public string CollectionName { get; set; } = "BackOfficeEU.ReportMessages";
    public int TtlDays { get; set; } = 30;
}


