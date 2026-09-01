namespace VeriScan.Api.Diagnostics;

public static class RequestTelemetryApplicationBuilderExtensions
{
    public static IApplicationBuilder UseVeriScanRequestTelemetry(this IApplicationBuilder applicationBuilder)
    {
        return applicationBuilder.UseMiddleware<RequestTelemetryMiddleware>();
    }
}
