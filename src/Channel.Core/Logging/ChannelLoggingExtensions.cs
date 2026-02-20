using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Channel.Core.Logging;

public static class ChannelLoggingExtensions
{
    public static IServiceCollection AddChannelCoreLogging(this IServiceCollection services)
    {
        services.AddSingleton<IChannelLogService, ChannelLogService>();
        return services;
    }

    public static ILoggingBuilder AddChannelCoreLogging(this ILoggingBuilder logging, IConfiguration configuration)
    {
        logging.ClearProviders();
        logging.AddConfiguration(configuration.GetSection("Logging"));
        logging.AddSimpleConsole(options =>
        {
            options.SingleLine = true;
            options.TimestampFormat = "HH:mm:ss ";
        });

        return logging;
    }
}
