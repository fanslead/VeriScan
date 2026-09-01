using OpenTelemetry.Exporter;

namespace VeriScan.Api.Diagnostics;

internal static class ObservabilityConfiguration
{
    public static Uri ResolveEndpoint(ObservabilityOptions options, IConfiguration configuration)
    {
        var endpoint = options.Otlp.Endpoint;
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            endpoint = configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];
        }

        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            || string.IsNullOrWhiteSpace(uri.Host)
            || !string.IsNullOrEmpty(uri.UserInfo)
            || !string.IsNullOrEmpty(uri.Query)
            || !string.IsNullOrEmpty(uri.Fragment))
        {
            throw new InvalidOperationException(
                "启用 OTLP 导出时必须配置不包含凭据、查询参数或片段的 HTTP(S) Endpoint。");
        }

        return uri;
    }

    public static OtlpExportProtocol ResolveProtocol(OtlpOptions options, IConfiguration configuration)
    {
        var protocol = string.IsNullOrWhiteSpace(options.Protocol)
            ? configuration["OTEL_EXPORTER_OTLP_PROTOCOL"]
            : options.Protocol;

        return protocol?.Trim().ToLowerInvariant() switch
        {
            "http/protobuf" or "httpprotobuf" or "http" => OtlpExportProtocol.HttpProtobuf,
            "grpc" or null or "" => OtlpExportProtocol.Grpc,
            _ => throw new InvalidOperationException(
                "OTLP Protocol 仅支持 grpc 或 http/protobuf。")
        };
    }
}
