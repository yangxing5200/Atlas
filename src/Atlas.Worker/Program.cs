using Atlas.Consumers.Orders;
using Atlas.Extensions.DependencyInjection;
using Atlas.Modules.BidOps;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddAtlasWorker(
    builder.Configuration,
    modules =>
    {
        modules.AddModuleAssembly(typeof(OrderPlacedEventConsumer).Assembly);
        modules.AddModulesFromAssembly(typeof(BidOpsModule).Assembly);
    });

var app = builder.Build();
await app.RunAsync();
