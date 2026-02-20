using System.Text.Json;
using Channels.Consumer.Abstractions;

namespace Channels.Api.Serialization;

public sealed class JsonMessageSerializer : IMessageSerializer
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    public string Serialize<T>(T value)
    {
        return JsonSerializer.Serialize(value, JsonOptions);
    }

    public T Deserialize<T>(string payload)
    {
        var result = JsonSerializer.Deserialize<T>(payload, JsonOptions);
        if (result is null)
        {
            throw new InvalidOperationException($"Unable to deserialize payload to {typeof(T).Name}.");
        }

        return result;
    }

    public IDictionary<string, string> NormalizeHeaders(IDictionary<string, string>? headers)
    {
        if (headers is null)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        return new Dictionary<string, string>(headers, StringComparer.OrdinalIgnoreCase);
    }
}


