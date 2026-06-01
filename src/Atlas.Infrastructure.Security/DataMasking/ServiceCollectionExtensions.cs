using Atlas.Core.DataMasking;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Atlas.Infrastructure.Security.DataMasking;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAtlasDataMasking(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<DataMaskingOptions>()
            .Bind(configuration.GetSection(DataMaskingOptions.SectionName))
            .ValidateOnStart();

        services.AddSingleton<ISensitiveValueMasker, SensitiveValueMasker>();
        services.AddScoped<IDataMaskingService, DataMaskingService>();
        services.AddScoped<DataMaskingResultFilter>();
        services.AddScoped<ISensitiveDataRevealExecutor, SensitiveDataRevealExecutor>();

        return services;
    }
}
