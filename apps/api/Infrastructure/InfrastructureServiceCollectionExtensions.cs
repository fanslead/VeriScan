using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using VeriScan.Application.Abstractions;
using VeriScan.Infrastructure.Persistence;
using VeriScan.Infrastructure.Persistence.Repositories;
using VeriScan.Infrastructure.Security;

namespace VeriScan.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddVeriScanInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("VeriScan") ?? string.Empty;

        services.AddDbContext<VeriScanDbContext>(options => options.UseNpgsql(
            connectionString,
            npgsql => npgsql.MigrationsAssembly(typeof(VeriScanDbContext).Assembly.FullName)));
        var redisConnectionString = configuration.GetConnectionString("Redis");
        if (!string.IsNullOrWhiteSpace(redisConnectionString))
        {
            services.AddStackExchangeRedisCache(options => options.Configuration = redisConnectionString);
        }

        services.AddHybridCache(options =>
        {
            options.MaximumKeyLength = 256;
            options.MaximumPayloadBytes = 64 * 1024;
            options.DefaultEntryOptions = new HybridCacheEntryOptions
            {
                Expiration = TimeSpan.FromMinutes(2),
                LocalCacheExpiration = TimeSpan.FromSeconds(15)
            };
        });
        services.AddOptions<ApiKeyOptions>()
            .Bind(configuration.GetSection(ApiKeyOptions.SectionName))
            .ValidateDataAnnotations()
            .Validate(
                options => Encoding.UTF8.GetByteCount(options.Pepper) >= 32,
                "Security:ApiKey:Pepper 至少需要 32 字节。")
            .Validate(
                options => options.PepperVersion.All(
                    character => char.IsLetterOrDigit(character) || character is '-' or '_' or '.'),
                "Security:ApiKey:PepperVersion 只能包含字母、数字、点、下划线或连字符。")
            .ValidateOnStart();
        services.AddScoped<IApplicationStore, ApplicationStore>();
        services.AddScoped<IApiKeyStore, ApiKeyStore>();
        services.AddScoped<IModerationStore, ModerationStore>();
        services.AddScoped<IAdminReadStore, AdminReadStore>();
        services.AddScoped<IApplicationUsageStore, ApplicationUsageStore>();
        services.AddScoped<IRuleSetStore, RuleSetStore>();
        services.AddScoped<IAiModelConfigurationStore, AiModelConfigurationStore>();
        services.AddScoped<IApiKeyMaterialGenerator, ApiKeyMaterialService>();
        services.AddScoped<IApiKeyVerifier, ApiKeyMaterialService>();
        services.AddSingleton<HybridApiKeyCache>();
        services.AddSingleton<IApiKeyCacheInvalidator>(serviceProvider =>
            serviceProvider.GetRequiredService<HybridApiKeyCache>());
        services.AddScoped<IApiKeyPolicy, ApiKeyPolicy>();
        services.AddSingleton<IContentHashService, ContentHashService>();
        services.AddScoped<DatabaseInitializer>();
        return services;
    }
}
