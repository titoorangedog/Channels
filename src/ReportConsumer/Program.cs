using ReportConsumer.Configuration;
using ReportConsumer.Services;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<ConsumerOptions>(builder.Configuration.GetSection(ConsumerOptions.SectionName));

var consumerOptions = builder.Configuration.GetSection(ConsumerOptions.SectionName).Get<ConsumerOptions>() ?? new ConsumerOptions();
builder.Services.AddHttpClient<QueueServiceClient>(client =>
{
    client.BaseAddress = new Uri(consumerOptions.QueueServiceBaseUrl);
});

builder.Services.AddSingleton<IReportRunnerService, ReportRunnerService>();
builder.Services.AddHostedService<ReportConsumerWorker>();

var host = builder.Build();
host.Run();
