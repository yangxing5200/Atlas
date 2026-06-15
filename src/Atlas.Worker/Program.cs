using Atlas.Consumers.Orders;
using Atlas.Extensions.DependencyInjection;
using Atlas.Infrastructure.Logging.Extensions;
using Atlas.Modules.BidOps;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddAtlasLogging(builder.Configuration);
builder.Logging.ClearProviders();
builder.Services.AddSerilog(Log.Logger, dispose: true);

builder.Services.AddAtlasWorker(
    builder.Configuration,
    modules =>
    {
        modules.AddModuleAssembly(typeof(OrderPlacedEventConsumer).Assembly);
        modules.AddModulesFromAssembly(typeof(BidOpsModule).Assembly);
    });

var app = builder.Build();

try
{
    await app.RunAsync();
}
finally
{
    Log.CloseAndFlush();
}
