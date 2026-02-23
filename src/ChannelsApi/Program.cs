using ChannelsApi.Configuration;
using ChannelsApi.Consumers;
using ChannelsApi.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<QueueOptions>(builder.Configuration.GetSection(QueueOptions.SectionName));

builder.Services.AddSingleton<IReportQueueService, ReportQueueService>();
builder.Services.AddScoped<IReportRunnerService, ReportRunnerService>();
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
