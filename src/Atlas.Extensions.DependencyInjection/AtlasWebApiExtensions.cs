using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Atlas.Core.Converter;
using Atlas.Core.Security;
using Atlas.Data.Tenant.Middleware;
using Atlas.Extensions.DependencyInjection.HealthChecks;
using Atlas.Infrastructure.Logging.Extensions;
using Atlas.Infrastructure.Security;
using Atlas.Infrastructure.Security.Permissions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
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
        return builder.AddAtlasWebApi(
            null,
            AtlasModuleCatalog.Empty,
            messagingConsumerAssemblies);
    }

    public static WebApplicationBuilder AddAtlasWebApi(
        this WebApplicationBuilder builder,
        Action<AtlasWebApiOptions>? configure,
        params Assembly[] messagingConsumerAssemblies)
    {
        return builder.AddAtlasWebApi(
            configure,
            AtlasModuleCatalog.Empty,
            messagingConsumerAssemblies);
    }

    public static WebApplicationBuilder AddAtlasWebApi(
        this WebApplicationBuilder builder,
        IEnumerable<IAtlasModule> modules)
    {
        return builder.AddAtlasWebApi(null, modules);
    }

    public static WebApplicationBuilder AddAtlasWebApi(
        this WebApplicationBuilder builder,
        Action<AtlasWebApiOptions>? configure,
        IEnumerable<IAtlasModule> modules)
    {
        return builder.AddAtlasWebApi(
            configure,
            AtlasModuleCatalog.Create(modules),
            Array.Empty<Assembly>());
    }

    public static WebApplicationBuilder AddAtlasWebApi(
        this WebApplicationBuilder builder,
        Action<AtlasWebApiOptions>? configure,
        Action<AtlasModuleRegistrationOptions> configureModules,
        params Assembly[] messagingConsumerAssemblies)
    {
        ArgumentNullException.ThrowIfNull(configureModules);

        var moduleOptions = new AtlasModuleRegistrationOptions();
        configureModules(moduleOptions);

        return builder.AddAtlasWebApi(
            configure,
            AtlasModuleCatalog.Create(moduleOptions.BuildModules()),
            messagingConsumerAssemblies);
    }

    private static WebApplicationBuilder AddAtlasWebApi(
        this WebApplicationBuilder builder,
        Action<AtlasWebApiOptions>? configure,
        AtlasModuleCatalog moduleCatalog,
        Assembly[] messagingConsumerAssemblies)
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

        builder.Services.AddOptions<CryptoOptions>()
            .Bind(builder.Configuration.GetSection("Security:Crypto"))
            .Validate(options => !string.IsNullOrWhiteSpace(options.Key), "Security:Crypto:Key is required.")
            .Validate(
                options => Encoding.UTF8.GetByteCount(options.Key) >= 32,
                "Security:Crypto:Key must be at least 32 UTF-8 bytes.")
            .ValidateOnStart();

        builder.Services.AddOptions<TokenOptions>()
            .Bind(builder.Configuration.GetSection("Security:Token"))
            .Validate(options => !string.IsNullOrWhiteSpace(options.SecretKey), "Security:Token:SecretKey is required.")
            .Validate(
                options => Encoding.UTF8.GetByteCount(options.SecretKey) >= 32,
                "Security:Token:SecretKey must be at least 32 UTF-8 bytes.")
            .Validate(options => options.ExpirationMinutes > 0, "Security:Token:ExpirationMinutes must be greater than 0.")
            .Validate(options => options.RefreshTokenExpirationDays > 0, "Security:Token:RefreshTokenExpirationDays must be greater than 0.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.CookieName), "Security:Token:CookieName is required.")
            .ValidateOnStart();

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
        builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionAuthorizationPolicyProvider>();
        builder.Services.AddScoped<IAuthorizationHandler, TenantAdminAuthorizationHandler>();
        builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();

        var mvcBuilder = builder.Services.AddControllers();
        foreach (var assembly in moduleCatalog.ControllerAssemblies)
        {
            mvcBuilder.AddApplicationPart(assembly);
        }

        mvcBuilder.AddJsonOptions(jsonOptions =>
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
        builder.Services.AddAtlasCore(
            builder.Configuration,
            AtlasModuleCatalog.CreateWithBuiltInModules(moduleCatalog.Modules),
            messagingConsumerAssemblies,
            AtlasRuntimeMode.WebApi);
        builder.Services.AddAtlasLogging(builder.Configuration);
        builder.Services.AddAtlasHealthChecks(builder.Configuration);

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
        app.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = registration => registration.Tags.Contains("live"),
            ResponseWriter = WriteHealthCheckResponseAsync
        });
        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = registration => registration.Tags.Contains("ready"),
            ResponseWriter = WriteHealthCheckResponseAsync
        });
        app.MapHealthChecks("/health", new HealthCheckOptions
        {
            Predicate = _ => true,
            ResponseWriter = WriteHealthCheckResponseAsync
        });
        app.MapControllers();

        return app;
    }

    public static IServiceCollection AddAtlasHealthChecks(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy("Process is running."), tags: ["live"])
            .AddCheck<AtlasGlobalDatabaseHealthCheck>("atlas-global-db", tags: ["ready", "mysql"])
            .AddCheck<AtlasCacheHealthCheck>("atlas-cache", tags: ["ready", "cache"])
            .AddCheck<AtlasRedisHealthCheck>("atlas-redis", tags: ["ready", "redis"])
            .AddCheck<AtlasRabbitMqHealthCheck>("atlas-rabbitmq", tags: ["ready", "rabbitmq"])
            .AddCheck<AtlasBackgroundJobHealthCheck>("atlas-background-jobs", tags: ["ready", "background-jobs"]);

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
