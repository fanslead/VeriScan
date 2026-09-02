using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using VeriScan.Application.Abstractions;
using VeriScan.Infrastructure.Jobs;
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
        var dataProtection = services.AddDataProtection()
            .SetApplicationName("VeriScan.ModerationContent.v1");
        var keyRingPath = configuration["Security:ContentProtection:KeyRingPath"];
        if (!string.IsNullOrWhiteSpace(keyRingPath))
        {
            Directory.CreateDirectory(keyRingPath);
            dataProtection.PersistKeysToFileSystem(new DirectoryInfo(keyRingPath));
        }
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
        services.AddOptions<ModerationQueueOptions>()
            .Bind(configuration.GetSection(ModerationQueueOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddOptions<ModerationIdempotencyOptions>()
            .Bind(configuration.GetSection(ModerationIdempotencyOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddOptions<ModerationDigestOptions>()
            .Bind(configuration.GetSection(ModerationDigestOptions.SectionName))
            .ValidateDataAnnotations()
            .Validate(
                options => !string.Equals(
                    options.ContentPepper,
                    options.IdempotencyPepper,
                    StringComparison.Ordinal),
                "内容摘要与幂等摘要必须使用不同的 pepper。")
            .ValidateOnStart();
        services.AddScoped<IApplicationStore, ApplicationStore>();
        services.AddScoped<IApiKeyStore, ApiKeyStore>();
        services.AddScoped<IModerationStore, ModerationStore>();
        services.AddScoped<IModerationJobStore, ModerationJobStore>();
        services.AddScoped<IModerationCancellationStore, ModerationCancellationStore>();
        services.AddScoped<IAdminReadStore, AdminReadStore>();
        services.AddScoped<IApplicationUsageStore, ApplicationUsageStore>();
        services.AddScoped<IUsageProjectionStore, UsageProjectionStore>();
        services.AddScoped<IOperationalFactStore, OperationalFactStore>();
        services.AddScoped<IOutboxStore, OutboxStore>();
        services.AddScoped<IAuditReadStore, AuditReadStore>();
        services.AddScoped<IRuleSetStore, RuleSetStore>();
        services.AddScoped<IAiModelConfigurationStore, AiModelConfigurationStore>();
        services.AddScoped<IApiKeyMaterialGenerator, ApiKeyMaterialService>();
        services.AddScoped<IApiKeyVerifier, ApiKeyMaterialService>();
        services.AddSingleton<HybridApiKeyCache>();
        services.AddSingleton<IApiKeyCacheInvalidator>(serviceProvider =>
            serviceProvider.GetRequiredService<HybridApiKeyCache>());
        services.AddScoped<IApiKeyPolicy, ApiKeyPolicy>();
        services.AddSingleton<IContentHashService, ContentHashService>();
        services.AddSingleton<IIdempotencyDigestService, IdempotencyDigestService>();
        services.AddSingleton<IModerationContentProtector, DataProtectionContentProtector>();
        services.AddSingleton<IModerationQueuePolicy, ModerationQueuePolicy>();
        services.AddSingleton<IModerationIdempotencyPolicy, ModerationIdempotencyPolicy>();
        services.AddScoped<DatabaseInitializer>();
        return services;
    }
}
