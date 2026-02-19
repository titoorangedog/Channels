using Channels.Api.Abstractions;
using Microsoft.Extensions.Caching.Memory;

namespace Channels.Api.Dedup;

public sealed class InMemoryDedupStore : IDedupStore
{
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());

    public bool TryStart(string messageId, TimeSpan ttl)
    {
        if (_cache.TryGetValue(messageId, out _))
        {
            return false;
        }

        _cache.Set(messageId, true, ttl);
        return true;
    }

    public void Complete(string messageId)
    {
        _cache.Remove(messageId);
    }
}
