using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using VeriScan.Application.Abstractions;
using VeriScan.Infrastructure.ExternalAi;

namespace VeriScan.Infrastructure;

public static class ExternalAiServiceCollectionExtensions
{
    public static IServiceCollection AddVeriScanExternalAi(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<ExternalAiOptions>()
            .Bind(configuration.GetSection(ExternalAiOptions.SectionName))
            .ValidateDataAnnotations()
            .Validate(
                options => options.AllowedPorts.All(port => port is > 0 and <= 65_535),
                "ExternalAi:AllowedPorts 必须是有效端口。")
            .Validate(
                options => options.ConnectTimeoutMs <= 30_000,
                "ExternalAi:ConnectTimeoutMs 不能超过 30000 毫秒。")
            .Validate(
                options => options.MaximumResponseBytes is >= 16_384 and <= 4 * 1024 * 1024,
                "ExternalAi:MaximumResponseBytes 必须在 16384 到 4194304 字节之间。")
            .ValidateOnStart();

        services.TryAddSingleton<IAiEndpointPolicy, ExternalAiEndpointPolicy>();
        services.TryAddSingleton<IExternalAiCredentialResolver, ExternalAiCredentialResolver>();
        services.TryAddSingleton<IAiSchemaDescriptor, ExternalAiSchemaDescriptor>();
        services.TryAddSingleton<IActiveAiConfigurationProvider, ActiveAiConfigurationProvider>();
        services.TryAddSingleton<IModerationExecutionPolicy, ModerationExecutionPolicy>();
        services.AddSingleton<ExternalAiHttpExecutor>();

        services.AddHttpClient<OpenAiChatCompletionsClient>()
            .ConfigurePrimaryHttpMessageHandler(serviceProvider =>
                ExternalAiNetworkGuard.CreateHandler(
                    serviceProvider.GetRequiredService<IOptionsMonitor<ExternalAiOptions>>().CurrentValue));
        services.AddHttpClient<OpenAiResponsesClient>()
            .ConfigurePrimaryHttpMessageHandler(serviceProvider =>
                ExternalAiNetworkGuard.CreateHandler(
                    serviceProvider.GetRequiredService<IOptionsMonitor<ExternalAiOptions>>().CurrentValue));
        services.AddHttpClient<AnthropicMessagesClient>()
            .ConfigurePrimaryHttpMessageHandler(serviceProvider =>
                ExternalAiNetworkGuard.CreateHandler(
                    serviceProvider.GetRequiredService<IOptionsMonitor<ExternalAiOptions>>().CurrentValue));

        services.TryAddScoped<IModerationAiClient, ExternalModerationAiClient>();
        services.TryAddScoped<IAiConfigurationProbe, ExternalAiConfigurationProbe>();
        return services;
    }
}
