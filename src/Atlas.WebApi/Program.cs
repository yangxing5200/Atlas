using Atlas.Extensions.DependencyInjection;
using Atlas.Modules.BidOps;

var builder = WebApplication.CreateBuilder(args);

builder.AddAtlasWebApi(options =>
{
    options.ApiTitle = "Atlas API";
}, modules => modules.AddModulesFromAssembly(typeof(BidOpsModule).Assembly));

var app = builder.Build();
app.UseAtlasWebApi(options =>
{
    options.ApiTitle = "Atlas API";
});

try
{
    await app.RunAsync();
}
finally
{
    Serilog.Log.CloseAndFlush();
}
