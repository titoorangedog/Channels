using System.Threading.Channels;
using Channels.Api.Abstractions;
using Channels.Api.Configuration;
using Channels.Api.Contracts;
using Channels.Api.Dedup;
using Channels.Api.Endpoints;
using Channels.Api.Persistence;
using Channels.Api.Pipeline;
using Channels.Api.Processing;
using Channels.Api.Queue;
using Channels.Api.Serialization;
using Channels.Api.Services;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<QueueOptions>(builder.Configuration.GetSection("Queue"));
builder.Services.Configure<PipelineOptions>(builder.Configuration.GetSection("Pipeline"));
builder.Services.Configure<MongoOptions>(builder.Configuration.GetSection("Mongo"));

builder.Services.AddSingleton<IMessageSerializer, JsonMessageSerializer>();
builder.Services.AddSingleton<IDedupStore, InMemoryDedupStore>();

builder.Services.AddSingleton(sp =>
{
    var pipelineOptions = sp.GetRequiredService<IOptions<PipelineOptions>>().Value;
    var capacity = pipelineOptions.ChannelCapacity <= 0 ? 500 : pipelineOptions.ChannelCapacity;
    return Channel.CreateBounded<QueueReceiveItem>(new BoundedChannelOptions(capacity)
    {
        FullMode = BoundedChannelFullMode.Wait,
        SingleWriter = true,
        SingleReader = false
    });
});

builder.Services.AddSingleton<IMongoClient>(sp =>
{
    var options = sp.GetRequiredService<IOptions<MongoOptions>>().Value;
    return new MongoClient(options.ConnectionString);
});

builder.Services.AddSingleton<IMessagesPersistenceStore>(sp =>
{
    var options = sp.GetRequiredService<IOptions<MongoOptions>>().Value;
    if (string.Equals(options.ConnectionString, "InMemory", StringComparison.OrdinalIgnoreCase))
    {
        return new InMemoryMessagesPersistenceStore();
    }

    return new MongoMessagesPersistenceStore(
        sp.GetRequiredService<IMongoClient>(),
        sp.GetRequiredService<IOptions<MongoOptions>>(),
        sp.GetRequiredService<IOptions<QueueOptions>>(),
        sp.GetRequiredService<ILogger<MongoMessagesPersistenceStore>>());
});

builder.Services.AddSingleton<IQueueClient>(sp =>
{
    var queueOptions = sp.GetRequiredService<IOptions<QueueOptions>>().Value;
    if (string.Equals(queueOptions.Provider, "InMemory", StringComparison.OrdinalIgnoreCase))
    {
        return new InMemoryQueueClient(sp.GetRequiredService<IMessageSerializer>());
    }

    return new AzureServiceBusQueueClient(
        sp.GetRequiredService<IOptions<QueueOptions>>(),
        sp.GetRequiredService<IOptions<PipelineOptions>>(),
        sp.GetRequiredService<IMessageSerializer>(),
        sp.GetRequiredService<ILogger<AzureServiceBusQueueClient>>());
});

builder.Services.AddSingleton<IMessageProcessor, DummyMessageProcessor>();
builder.Services.AddSingleton<QueueMessageHandler>();
builder.Services.AddSingleton<QueueMoveService>();

builder.Services.AddHostedService<MongoIndexesInitializerHostedService>();
builder.Services.AddHostedService<ProducerBackgroundService>();
builder.Services.AddHostedService<ConsumerPoolBackgroundService>();

var app = builder.Build();
app.MapQueueEndpoints();
app.Run();

public partial class Program;
