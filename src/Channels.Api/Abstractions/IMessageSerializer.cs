namespace Channels.Api.Abstractions;

public interface IMessageSerializer
{
    string Serialize<T>(T value);
    T Deserialize<T>(string payload);
    IDictionary<string, string> NormalizeHeaders(IDictionary<string, string>? headers);
}
