using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Atlas.Core.Converter;
using Atlas.Core.Security;
using Atlas.Data.Tenant.Middleware;
using Atlas.Extensions.DependencyInjection.HealthChecks;
using Atlas.Infrastructure.Logging.Extensions;
using Atlas.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using Serilog;

namespace Atlas.Extensions.DependencyInjection;

public static class AtlasWebApiExtensions
{
    private const string AuthenticationScheme = "CustomToken";

    public static WebApplicationBuilder AddAtlasWebApi(
        this WebApplicationBuilder builder,
        params Assembly[] messagingConsumerAssemblies)
    {
        return builder.AddAtlasWebApi(null, messagingConsumerAssemblies);
    }

    public static WebApplicationBuilder AddAtlasWebApi(
        this WebApplicationBuilder builder,
        Action<AtlasWebApiOptions>? configure,
        params Assembly[] messagingConsumerAssemblies)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var options = new AtlasWebApiOptions();
        configure?.Invoke(options);

        builder.Host.UseSerilog();
        builder.Services.AddMemoryCache(cacheOptions => cacheOptions.CompactionPercentage = 0.25);
        builder.Services.AddProblemDetails();
        builder.Services.AddExceptionHandler<AtlasExceptionHandler>();
        builder.Services.Configure<ApiBehaviorOptions>(apiOptions =>
        {
            apiOptions.InvalidModelStateResponseFactory = context =>
            {
                var problem = new ValidationProblemDetails(context.ModelState)
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Validation failed",
                    Instance = context.HttpContext.Request.Path
                };
                problem.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
                return new BadRequestObjectResult(problem);
            };
        });

        builder.Services.Configure<CryptoOptions>(cryptoOptions =>
        {
            cryptoOptions.Key = builder.Configuration["Security:Crypto:Key"]
                ?? throw new InvalidOperationException("Security:Crypto:Key is required.");
        });

        builder.Services.Configure<TokenOptions>(tokenOptions =>
        {
            tokenOptions.SecretKey = builder.Configuration["Security:Token:SecretKey"]
                ?? throw new InvalidOperationException("Security:Token:SecretKey is required.");
            tokenOptions.ExpirationMinutes = builder.Configuration.GetValue<int>("Security:Token:ExpirationMinutes", 1440);
            tokenOptions.CookieName = builder.Configuration["Security:Token:CookieName"] ?? "atlas-auth-token";
        });

        builder.Services.AddSingleton<ICryptoService, CryptoService>();
        builder.Services.AddSingleton<ITokenService, CustomTokenService>();
        builder.Services.AddScoped<ITokenCacheService, TokenCacheService>();

        builder.Services.AddAuthentication(AuthenticationScheme)
            .AddScheme<CustomTokenAuthenticationOptions, CustomTokenAuthenticationHandler>(
                AuthenticationScheme,
                authOptions =>
                {
                    authOptions.TokenHeaderName = "Authorization";
                    authOptions.TokenPrefix = "Bearer";
                    authOptions.EnableQueryStringToken = builder.Configuration.GetValue(
                        "Security:Token:EnableQueryStringToken",
                        false);
                    authOptions.EnableCustomHeader = builder.Configuration.GetValue(
                        "Security:Token:EnableCustomHeader",
                        true);
                    authOptions.CookieName = builder.Configuration["Security:Token:CookieName"] ?? "atlas-auth-token";
                    authOptions.LoginPath = builder.Configuration["Security:LoginPath"] ?? "/login";
                });

        builder.Services.AddAuthorization(authorizationOptions =>
        {
            authorizationOptions.FallbackPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();

            authorizationOptions.AddPolicy(AuthorizationPolicies.RequireTenantAdmin, policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.AddRequirements(new TenantAdminRequirement());
            });
        });
        builder.Services.AddScoped<IAuthorizationHandler, TenantAdminAuthorizationHandler>();

        builder.Services
            .AddControllers()
            .AddJsonOptions(jsonOptions =>
            {
                jsonOptions.JsonSerializerOptions.Converters.Add(new JsonNumberConverter());
                jsonOptions.JsonSerializerOptions.Converters.Add(new NullableJsonNumberConverter());
                jsonOptions.JsonSerializerOptions.Converters.Add(new Iso8601DateTimeConverter());
                jsonOptions.JsonSerializerOptions.Converters.Add(new NullableIso8601DateTimeConverter());
                jsonOptions.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
                jsonOptions.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
                jsonOptions.JsonSerializerOptions.AllowTrailingCommas = true;
                jsonOptions.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
            });

        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(swaggerOptions =>
        {
            swaggerOptions.SwaggerDoc(options.ApiVersion, new OpenApiInfo
            {
                Version = options.ApiVersion,
                Title = options.ApiTitle,
                Description = "Atlas multi-tenant Web API"
            });

            swaggerOptions.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Name = "Authorization",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.ApiKey,
                Scheme = "Bearer",
                BearerFormat = "Custom Token",
                Description = "Enter: Bearer {token}"
            });
            swaggerOptions.AddSecurityRequirement(new OpenApiSecurityRequirement
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
        });

        AddCors(builder, options.CorsPolicyName);
        builder.Services.AddAtlasCore(builder.Configuration, messagingConsumerAssemblies);
        builder.Services.AddAtlasLogging(builder.Configuration);
        builder.Services.AddAtlasHealthChecks();

        return builder;
    }

    public static WebApplication UseAtlasWebApi(
        this WebApplication app,
        Action<AtlasWebApiOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(app);

        var options = new AtlasWebApiOptions();
        configure?.Invoke(options);

        app.UseExceptionHandler();

        if (options.EnableSwagger && app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI(swaggerOptions =>
            {
                swaggerOptions.SwaggerEndpoint($"/swagger/{options.ApiVersion}/swagger.json", $"{options.ApiTitle} {options.ApiVersion}");
                swaggerOptions.RoutePrefix = "swagger";
            });
        }

        if (options.EnableHttpsRedirection)
            app.UseHttpsRedirection();

        app.UseCors(options.CorsPolicyName);
        app.UseAuthentication();
        app.UseMiddleware<TenantConnectionPreloadMiddleware>();
        app.UseMiddleware<TokenVersionValidationMiddleware>();
        app.UseAuthorization();
        app.UseAtlasLogging();
        app.MapHealthChecks("/health", new HealthCheckOptions
        {
            ResponseWriter = WriteHealthCheckResponseAsync
        });
        app.MapControllers();

        return app;
    }

    public static IServiceCollection AddAtlasHealthChecks(this IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddCheck<AtlasGlobalDatabaseHealthCheck>("atlas-global-db")
            .AddCheck<AtlasCacheHealthCheck>("atlas-cache")
            .AddCheck<AtlasBackgroundJobHealthCheck>("atlas-background-jobs");

        return services;
    }

    private static void AddCors(WebApplicationBuilder builder, string policyName)
    {
        builder.Services.AddCors(corsOptions =>
        {
            corsOptions.AddPolicy(policyName, policy =>
            {
                var allowedOrigins = builder.Configuration
                    .GetSection("Cors:AllowedOrigins")
                    .Get<string[]>() ?? Array.Empty<string>();

                if (allowedOrigins.Length > 0)
                {
                    policy.WithOrigins(allowedOrigins)
                        .AllowAnyHeader()
                        .AllowAnyMethod()
                        .AllowCredentials();
                    return;
                }

                if (builder.Environment.IsDevelopment())
                {
                    policy.AllowAnyOrigin()
                        .AllowAnyHeader()
                        .AllowAnyMethod();
                    return;
                }

                policy.SetIsOriginAllowed(_ => false);
            });
        });
    }

    private static Task WriteHealthCheckResponseAsync(HttpContext context, Microsoft.Extensions.Diagnostics.HealthChecks.HealthReport report)
    {
        context.Response.ContentType = "application/json; charset=utf-8";

        var payload = new
        {
            status = report.Status.ToString(),
            totalDuration = report.TotalDuration.TotalMilliseconds,
            checks = report.Entries.Select(entry => new
            {
                name = entry.Key,
                status = entry.Value.Status.ToString(),
                entry.Value.Description,
                duration = entry.Value.Duration.TotalMilliseconds
            })
        };

        return context.Response.WriteAsync(JsonSerializer.Serialize(payload));
    }
}
