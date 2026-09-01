using System.Globalization;
using Microsoft.Extensions.Options;
using VeriScan.Api.RateLimiting;

namespace VeriScan.Api.Middleware;

public sealed class RateLimitHeadersMiddleware(
    RequestDelegate next,
    IOptions<RateLimitingOptions> options)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (options.Value.Enabled && context.Request.Path.StartsWithSegments(options.Value.PathPrefix))
        {
            context.Response.OnStarting(() =>
            {
                var responseHeaders = context.Response.Headers;
                if (!responseHeaders.ContainsKey("RateLimit-Limit"))
                {
                    responseHeaders["RateLimit-Limit"] = RateLimitingServiceCollectionExtensions
                        .GetEffectiveLimit(context.User, options.Value)
                        .ToString(CultureInfo.InvariantCulture);
                }

                if (!responseHeaders.ContainsKey("RateLimit-Reset"))
                {
                    responseHeaders["RateLimit-Reset"] = RateLimitingServiceCollectionExtensions
                        .GetEffectiveWindowSeconds(context.User, options.Value)
                        .ToString(CultureInfo.InvariantCulture);
                }

                return Task.CompletedTask;
            });
        }

        await next(context);
    }
}

public static class RateLimitHeadersApplicationBuilderExtensions
{
    public static IApplicationBuilder UseVeriScanRateLimitHeaders(this IApplicationBuilder app)
    {
        return app.UseMiddleware<RateLimitHeadersMiddleware>();
    }
}
