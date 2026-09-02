using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using VeriScan.Application.Abstractions;
using VeriScan.Infrastructure.Webhooks;

namespace VeriScan.Infrastructure;

/// <summary>注册 Webhook 供应商适配器及其配置。</summary>
public static class WebhookServiceCollectionExtensions
{
    public static IServiceCollection AddVeriScanWebhooks(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<WebhookProviderOptions>()
            .Bind(configuration.GetSection(WebhookProviderOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<IWebhookProvider, SvixWebhookProvider>();
        return services;
    }
}
