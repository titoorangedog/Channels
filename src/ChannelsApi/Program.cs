using ChannelsApi.Configuration;
using ChannelsApi.Consumers;
using ChannelsApi.Services;

public partial class Program
{
    private static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.Configure<QueueOptions>(builder.Configuration.GetSection(QueueOptions.SectionName));

        // Report queue is an application-wide queue -> singleton
        builder.Services.AddSingleton<IReportQueueService, ReportQueueService>();

        // ReportRunnerService has no scoped dependencies (logger only).
        // Registering as singleton resolves the lifetime mismatch with the hosted service.
        builder.Services.AddSingleton<IReportRunnerService, ReportRunnerService>();

        // Hosted services are registered as singletons by AddHostedService.
        builder.Services.AddHostedService<ReportQueueConsumerService>();

        builder.Services.AddControllers();
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();

        var app = builder.Build();

        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.MapControllers();

        app.Run();
    }
}