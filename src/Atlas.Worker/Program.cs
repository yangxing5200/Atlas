using Atlas.Consumers.Orders;
using Atlas.Extensions.DependencyInjection;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddAtlasCore(
    builder.Configuration,
    modules => modules.AddModuleAssembly(typeof(OrderPlacedEventConsumer).Assembly));

var app = builder.Build();
await app.RunAsync();
