using Microsoft.Extensions.Logging;

namespace Channel.Core.Logging;

public sealed class ChannelLogService : IChannelLogService
{
    private readonly ILoggerFactory _loggerFactory;

    public ChannelLogService(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
    }

    public void Information(string category, string messageTemplate, params object?[] args)
    {
        _loggerFactory.CreateLogger(category).LogInformation(messageTemplate, args);
    }

    public void Warning(string category, string messageTemplate, params object?[] args)
    {
        _loggerFactory.CreateLogger(category).LogWarning(messageTemplate, args);
    }

    public void Error(string category, Exception exception, string messageTemplate, params object?[] args)
    {
        _loggerFactory.CreateLogger(category).LogError(exception, messageTemplate, args);
    }
}
