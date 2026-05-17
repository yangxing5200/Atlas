using Atlas.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.AddAtlasWebApi(options =>
{
    options.ApiTitle = "Atlas API";
});

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
