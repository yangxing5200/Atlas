using Atlas.Core.Security;
using Atlas.Data.Tenant.Middleware;
using Atlas.Extensions.DependencyInjection;
using Atlas.Infrastructure.Logging.Configuration;
using Atlas.Infrastructure.Logging.Extensions;
using Atlas.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.OpenApi.Models;
using Serilog;
using System.Text.Json.Serialization;
using System.Text.Json;
using Atlas.Core.Converter;
using Atlas.Sample.WebApi.Security;
using Atlas.Sample.WebApi.Consumers;
using Microsoft.AspNetCore.Authorization;
var builder = WebApplication.CreateBuilder(args);

// ============================================
// 1. 配置 Serilog
// ============================================
builder.Host.UseSerilog();

// ============================================
// 2. 添加内存缓存服务（必需）
// ============================================
builder.Services.AddMemoryCache(options =>
{
    options.CompactionPercentage = 0.25; // 当达到限制时压缩25%
});

// ============================================
// 3. 配置安全相关服务
// ============================================

// 配置加密服务选项
builder.Services.Configure<CryptoOptions>(options =>
{
    // ✅ 从配置文件读取密钥（生产环境应使用 Azure Key Vault 等）
    options.Key = builder.Configuration["Security:Crypto:Key"]
        ?? throw new InvalidOperationException("Crypto key not configured");
});

// 配置Token服务选项
builder.Services.Configure<TokenOptions>(options =>
{
    options.SecretKey = builder.Configuration["Security:Token:SecretKey"]
        ?? throw new InvalidOperationException("Token secret key not configured");
    options.ExpirationMinutes = builder.Configuration.GetValue<int>("Security:Token:ExpirationMinutes", 1440);
    options.CookieName = builder.Configuration["Security:Token:CookieName"] ?? "atlas-auth-token";
});

// 注册加密服务（Singleton - 性能优化）
builder.Services.AddSingleton<ICryptoService, CryptoService>();

// 注册Token服务（Singleton - 性能优化）
builder.Services.AddSingleton<ITokenService, CustomTokenService>();

// ✅ 配置自定义Token认证
builder.Services.AddAuthentication("CustomToken")
    .AddScheme<CustomTokenAuthenticationOptions, CustomTokenAuthenticationHandler>(
        "CustomToken",
        options =>
        {
            options.TokenHeaderName = "Authorization";
            options.TokenPrefix = "Bearer";
            options.EnableQueryStringToken = true;
            options.EnableCustomHeader = true;
            options.CookieName = builder.Configuration["Security:Token:CookieName"] ?? "atlas-auth-token";
            options.LoginPath = builder.Configuration["Security:LoginPath"] ?? "/login";
        });

// 配置授权策略
builder.Services.AddAuthorization(options =>
{
    // 默认策略：需要认证
    options.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();

    options.AddPolicy(AuthorizationPolicies.RequireTenantAdmin, policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.AddRequirements(new TenantAdminRequirement());
    });
});

builder.Services.AddScoped<IAuthorizationHandler, TenantAdminAuthorizationHandler>();

// ============================================
// 4. Add Atlas Core Services
// ============================================
builder.Services.AddAtlasCore(builder.Configuration, typeof(OrderPlacedEventConsumer).Assembly);
builder.Services.AddAtlasLogging(builder.Configuration);

// ============================================
// 5. Add Controllers
// ============================================
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // Long 转 String（解决雪花算法ID问题）
        options.JsonSerializerOptions.Converters.Add(new JsonNumberConverter());
        options.JsonSerializerOptions.Converters.Add(new NullableJsonNumberConverter());

        // ISO 8601 日期时间格式
        options.JsonSerializerOptions.Converters.Add(new Iso8601DateTimeConverter());
        options.JsonSerializerOptions.Converters.Add(new NullableIso8601DateTimeConverter());

        // 驼峰命名
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;

        // 忽略 null 值
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;

        // 允许尾随逗号
        options.JsonSerializerOptions.AllowTrailingCommas = true;

        // 宽松的属性名称匹配
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
    });

// ============================================
// 6. Add API Explorer & Swagger
// ============================================
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Version = "v1",
        Title = "Atlas API",
        Description = "Atlas Multi-Tenant Web API with Custom Token Authentication",
        Contact = new OpenApiContact
        {
            Name = "Atlas Team",
            Email = "support@atlas.com"
        }
    });

    // ✅ 配置Bearer Token认证（更详细）
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = @"JWT Authorization header using the Bearer scheme. 
                        Enter 'Bearer' [space] and then your token in the text input below.
                        Example: 'Bearer 1.1234567890.abc...xyz'",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer",
        BearerFormat = "Custom Token"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });

    // Enable XML comments if available
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }
});

// ============================================
// 7. Add AutoMapper
// ============================================
builder.Services.AddAutoMapper(_ => { }, AppDomain.CurrentDomain.GetAssemblies());

// ============================================
// 8. Add CORS
// ============================================
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

builder.Services.AddScoped<ITokenCacheService, TokenCacheService>();
// ============================================
// 9. Build Application
// ============================================
var app = builder.Build();

// ============================================
// 10. Configure the HTTP request pipeline
// ============================================
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Atlas API V1");
        options.RoutePrefix = "swagger"; // Set Swagger UI at app's root
    });
}

app.UseHttpsRedirection();

app.UseCors("AllowAll");

// ✅ 认证和授权中间件必须按顺序
app.UseAuthentication();  // 先认证
// ✅ 中间件顺序很重要！
app.UseMiddleware<TenantConnectionPreloadMiddleware>();
app.UseMiddleware<TokenVersionValidationMiddleware>();
app.UseAuthorization();   // 后授权

app.UseAtlasLogging();

app.MapControllers();

// ============================================
// 11. Run Application
// ============================================
try
{
    Log.Information("Starting Atlas Web API with Custom Token Authentication");
    Log.Information("Token Expiration: {Minutes} minutes",
        builder.Configuration.GetValue<int>("Security:Token:ExpirationMinutes", 1440));

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
