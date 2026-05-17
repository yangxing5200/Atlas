using Atlas.Extensions.DependencyInjection;
using Atlas.Sample.WebApi.Controllers;

var builder = WebApplication.CreateBuilder(args);

builder.AddAtlasWebApi(options =>
{
    options.ApiTitle = "Atlas Sample API";
});
builder.Services.AddScoped<IScopeDemoService, ScopeDemoService>();

var app = builder.Build();
app.UseAtlasWebApi(options =>
{
    options.ApiTitle = "Atlas Sample API";
});

try
{
    await app.RunAsync();
}
finally
{
    Serilog.Log.CloseAndFlush();
}
