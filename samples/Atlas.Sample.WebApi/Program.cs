using Atlas.Extensions.DependencyInjection;
using Atlas.Infrastructure.Http.Extensions;
using Atlas.Sample.ECommerce;
using Atlas.Sample.WebApi.Controllers;
using Atlas.Sample.WebApi.Integrations.PaymentX;
using Atlas.Sample.WebApi.Integrations.SampleThirdParty;
using Atlas.Sample.WebApi.Services.ExternalHttpDemo;
using Atlas.Sample.WebApi.Services.PaymentDemo;

var builder = WebApplication.CreateBuilder(args);

builder.AddAtlasWebApi(options =>
{
    options.ApiTitle = "Atlas Sample API";
}, modules =>
{
    modules.AddModule<SampleECommerceModule>();
});
builder.Services.AddScoped<IScopeDemoService, ScopeDemoService>();
builder.Services.AddScoped<IProductSourcingQueryService, ProductSourcingQueryService>();
builder.Services.AddScoped<IOrderPaymentDemoService, OrderPaymentDemoService>();
builder.Services.AddAtlasExternalHttpClient<ISampleThirdPartyClient, SampleThirdPartyClient>(
    builder.Configuration,
    SampleThirdPartyClient.ClientName);
builder.Services.Configure<PaymentXOptions>(
    builder.Configuration.GetSection(PaymentXOptions.SectionName));
builder.Services.AddTransient<PaymentXSignatureHandler>();
builder.Services
    .AddAtlasExternalHttpClient<IPaymentXClient, PaymentXClient>(
        builder.Configuration,
        PaymentXClient.ClientName)
    .AddHttpMessageHandler<PaymentXSignatureHandler>();

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
