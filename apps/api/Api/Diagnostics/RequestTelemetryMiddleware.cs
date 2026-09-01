using System.Diagnostics;
using Microsoft.Extensions.Options;

namespace VeriScan.Api.Diagnostics;

public sealed class RequestTelemetryMiddleware(
    RequestDelegate next,
    ILogger<RequestTelemetryMiddleware> logger,
    RequestMetrics metrics,
    IOptionsMonitor<ObservabilityOptions> options)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var currentOptions = options.CurrentValue;
        if (RequestTelemetryMetadata.IsExcluded(context, currentOptions))
        {
            await next(context);
            return;
        }

        var startedAt = Stopwatch.GetTimestamp();
        try
        {
            await next(context);
        }
        catch
        {
            if (!context.Response.HasStarted)
            {
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            }

            throw;
        }
        finally
        {
            var elapsedMilliseconds = Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;
            var route = RequestTelemetryMetadata.ResolveRoute(context);
            var statusCode = context.Response.StatusCode;
            Activity.Current?.SetTag("http.route", route);
            Activity.Current?.SetTag("http.response.status_code", statusCode);
            metrics.RecordRequest(context.Request.Method, route, statusCode, elapsedMilliseconds);
            if (logger.IsEnabled(LogLevel.Information))
            {
                var traceId = Activity.Current?.TraceId.ToHexString() ?? string.Empty;
                RequestTelemetryLog.Completed(
                    logger,
                    context.Request.Method,
                    route,
                    statusCode,
                    elapsedMilliseconds,
                    traceId);
            }
        }
    }
}

internal static partial class RequestTelemetryLog
{
    [LoggerMessage(
        EventId = 2000,
        Level = LogLevel.Information,
        Message = "HTTP 请求完成，方法={HttpMethod}，路由={Route}，状态码={StatusCode}，耗时毫秒={ElapsedMilliseconds}，TraceId={TraceId}。")]
    public static partial void Completed(
        ILogger logger,
        string httpMethod,
        string route,
        int statusCode,
        double elapsedMilliseconds,
        string traceId);
}
