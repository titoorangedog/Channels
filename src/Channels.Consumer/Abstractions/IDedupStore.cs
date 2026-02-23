namespace Channels.Consumer.Abstractions;

public interface IDedupStore
{
    bool TryStart(string messageId, TimeSpan ttl);
    void Complete(string messageId);
}


