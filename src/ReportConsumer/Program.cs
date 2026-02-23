using System;
using ReportConsumer.Configuration;
using ReportConsumer.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;

internal class Program
{
    private static void Main(string[] args)
    {
        var host = Host.CreateDefaultBuilder(args)
            .ConfigureServices((context, services) =>
            {
                // Prefer configured value; if not present (e.g. running Production), fallback to the queue service port observed in your logs.
                var baseUrl = context.Configuration["QueueService:BaseUrl"];
                if (string.IsNullOrWhiteSpace(baseUrl))
                {
                    baseUrl = "http://localhost:65044";
                }

                services.AddHttpClient<QueueServiceClient>(client =>
                {
                    client.BaseAddress = new Uri(baseUrl);

                    // Allow long-polling on the dequeue endpoint; rely on CancellationToken for cooperative cancellation.
                    client.Timeout = Timeout.InfiniteTimeSpan;
                });

                services.Configure<ConsumerOptions>(context.Configuration.GetSection(ConsumerOptions.SectionName));

                services.AddSingleton<IReportRunnerService, ReportRunnerService>();

                // Do not register QueueServiceClient as singleton here; AddHttpClient<T> registers the typed client.

                services.AddHostedService<ReportConsumerWorker>();

                // Debugging helper: keep host running when background exceptions occur.
                // Remove or change in production.
                services.Configure<HostOptions>(opts =>
                {
                    opts.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore;
                });
            })
            .Build();

        host.Run();
    }
}
