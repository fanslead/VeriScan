using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using VeriScan.Infrastructure.ExternalAi;

namespace VeriScan.Api.Diagnostics;

public static class ObservabilityServiceCollectionExtensions
{
    public static IServiceCollection AddVeriScanObservability(
        this IServiceCollection services,
        IConfiguration configuration,
        string serviceName)
    {
        var section = configuration.GetSection(ObservabilityOptions.SectionName);
        var options = section.Get<ObservabilityOptions>() ?? new ObservabilityOptions();
        services.Configure<ObservabilityOptions>(section);
        services.AddSingleton<RequestMetrics>();

        var telemetry = services
            .AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(serviceName: serviceName))
            .WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation(instrumentation =>
                {
                    instrumentation.Filter = context =>
                        !RequestTelemetryMetadata.IsExcluded(context, options);
                    instrumentation.RecordException = true;
                })
                .AddHttpClientInstrumentation(instrumentation =>
                {
                    instrumentation.RecordException = true;
                }))
            .WithMetrics(metrics => metrics
                .AddHttpClientInstrumentation()
                .AddMeter(RequestMetrics.MeterName)
                .AddMeter(ExternalAiMetrics.MeterName))
            .WithLogging(
                configureBuilder: _ => { },
                configureOptions: logging =>
                {
                    logging.IncludeScopes = true;
                    logging.IncludeFormattedMessage = true;
                });

        if (options.Otlp.Enabled)
        {
            var endpoint = ObservabilityConfiguration.ResolveEndpoint(options, configuration);
            var protocol = ObservabilityConfiguration.ResolveProtocol(options.Otlp, configuration);
            telemetry.UseOtlpExporter(protocol, endpoint);
        }

        return services;
    }
}
