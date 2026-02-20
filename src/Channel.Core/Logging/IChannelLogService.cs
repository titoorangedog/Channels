namespace Channel.Core.Logging;

public interface IChannelLogService
{
    void Information(string category, string messageTemplate, params object?[] args);
    void Warning(string category, string messageTemplate, params object?[] args);
    void Error(string category, Exception exception, string messageTemplate, params object?[] args);
}
