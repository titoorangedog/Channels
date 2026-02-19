namespace Channels.Api.Abstractions;

public interface IDedupStore
{
    bool TryStart(string messageId, TimeSpan ttl);
    void Complete(string messageId);
}
