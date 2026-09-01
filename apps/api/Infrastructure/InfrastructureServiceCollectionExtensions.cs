using System.Text;
using Microsoft.EntityFrameworkCore;
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
        services.AddScoped<IWordRuleStore, WordRuleStore>();
        services.AddScoped<IAiModelConfigurationStore, AiModelConfigurationStore>();
        services.AddScoped<IApiKeyMaterialGenerator, ApiKeyMaterialService>();
        services.AddScoped<IApiKeyVerifier, ApiKeyMaterialService>();
        services.AddScoped<IApiKeyPolicy, ApiKeyPolicy>();
        services.AddSingleton<IContentHashService, ContentHashService>();
        services.AddScoped<DatabaseInitializer>();
        return services;
    }
}
