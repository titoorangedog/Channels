using Channels.Api.Configuration;
using Channel.Core.Logging;
using Channels.Consumer.Abstractions;
using Channels.Consumer.Configuration;
using Channels.Api.Dedup;
using Channels.Api.Endpoints;
using Channels.Api.Persistence;
using Channels.Api.Processing;
using Channels.Consumer.Pipeline;
using Channels.Consumer.Processing;
using Channels.Api.Services;
using Channels.Producer.Extensions;
using Channels.Producer.Configuration;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<ApiHostOptions>(builder.Configuration.GetSection("Host"));
builder.Services.Configure<PipelineOptions>(builder.Configuration.GetSection("Pipeline"));
builder.Logging.AddChannelCoreLogging(builder.Configuration);
builder.Services.AddChannelCoreLogging();

builder.Services.AddHttpLogging(options =>
{
    options.LoggingFields =
        HttpLoggingFields.RequestMethod |
        HttpLoggingFields.RequestPath |
        HttpLoggingFields.ResponseStatusCode |
        HttpLoggingFields.Duration;
});

builder.Services.AddSingleton<IDedupStore, InMemoryDedupStore>();
builder.Services.AddChannelsProducer(builder.Configuration);

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

builder.Services.AddSingleton<IMessageProcessor, DummyMessageProcessor>();
builder.Services.AddSingleton<QueueMessageHandler>();
builder.Services.AddSingleton<QueueMoveService>();

builder.Services.AddHostedService<MongoIndexesInitializerHostedService>();
builder.Services.AddHostedService<ConsumerPoolBackgroundService>();

var app = builder.Build();
var hostOptions = app.Services.GetRequiredService<IOptions<ApiHostOptions>>().Value;
var sharedLog = app.Services.GetRequiredService<IChannelLogService>();
app.Urls.Clear();
app.Urls.Add(hostOptions.Url);
app.UseHttpLogging();
app.Use(async (context, next) =>
{
    var start = DateTimeOffset.UtcNow;
    Console.WriteLine($"[REQ] {context.Request.Method} {context.Request.Path}");
    await next();
    var elapsed = DateTimeOffset.UtcNow - start;
    Console.WriteLine($"[RES] {context.Request.Method} {context.Request.Path} -> {context.Response.StatusCode} ({elapsed.TotalMilliseconds:F0} ms)");
});
app.MapQueueEndpoints();

var consoleLogger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("WorkerConsole");
app.Lifetime.ApplicationStarted.Register(() =>
{
    Console.WriteLine($"[WORKER] Channels.Api started on {hostOptions.Url}");
    consoleLogger.LogInformation("Channels.Api worker started on {Url}", hostOptions.Url);
    sharedLog.Information("WorkerConsole", "Channels.Api shared logger started on {Url}", hostOptions.Url);
});
app.Lifetime.ApplicationStopping.Register(() =>
{
    Console.WriteLine("[WORKER] Channels.Api stopping.");
    consoleLogger.LogInformation("Channels.Api worker stopping.");
    sharedLog.Warning("WorkerConsole", "Channels.Api shared logger stopping.");
});

app.Run();

public partial class Program;


