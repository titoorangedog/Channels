namespace Channels.Api.Domain;

public class InfoExecutionModelBase
{
    private string _id = Guid.NewGuid().ToString("N");

    public string Id
    {
        get => string.IsNullOrWhiteSpace(_id) ? Guid.NewGuid().ToString("N") : _id;
        set => _id = string.IsNullOrWhiteSpace(value) ? Guid.NewGuid().ToString("N") : value;
    }

    public string User { get; set; } = string.Empty;
    public DateTimeOffset RequestedAt { get; set; } = DateTimeOffset.UtcNow;
    public IDictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();
}

