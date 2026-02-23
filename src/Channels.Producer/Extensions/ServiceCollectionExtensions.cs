using System.Threading.Channels;
using Channels.Consumer.Abstractions;
using Channels.Consumer.Configuration;
using Channels.Consumer.Contracts;
using Channels.Producer.Configuration;
using Channels.Producer.Pipeline;
using Channels.Producer.Queue;
using Channels.Producer.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Channels.Producer.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddChannelsProducer(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<QueueOptions>(configuration.GetSection("Queue"));
        services.Configure<MongoOptions>(configuration.GetSection("Mongo"));

        services.AddSingleton<IMessageSerializer, JsonMessageSerializer>();

        services.AddSingleton(sp =>
        {
            var pipelineOptions = sp.GetRequiredService<IOptions<PipelineOptions>>().Value;
            var capacity = pipelineOptions.ChannelCapacity <= 0 ? 500 : pipelineOptions.ChannelCapacity;
            return global::System.Threading.Channels.Channel.CreateBounded<QueueReceiveItem>(new BoundedChannelOptions(capacity)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleWriter = true,
                SingleReader = false
            });
        });

        services.AddSingleton<IQueueClient>(sp => new InMemoryQueueClient(
            sp.GetRequiredService<IMessageSerializer>(),
            sp.GetRequiredService<IOptions<QueueOptions>>()));

        services.AddHostedService<ProducerBackgroundService>();

        return services;
    }
}
