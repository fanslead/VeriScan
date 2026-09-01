using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using VeriScan.Api.Diagnostics;
using VeriScan.Api.RateLimiting;

namespace VeriScan.Api.Middleware;

/// <summary>
/// 在认证和缓存查询之前限制审核入口总并发，避免突发连接先耗尽认证链路资源。
/// </summary>
public sealed class IngressConcurrencyMiddleware : IDisposable
{
    private readonly RequestDelegate next;
    private readonly RateLimitingOptions options;
    private readonly RequestMetrics metrics;
    private readonly SemaphoreSlim permits;

    public IngressConcurrencyMiddleware(
        RequestDelegate next,
        IOptions<RateLimitingOptions> options,
        RequestMetrics metrics)
    {
        this.next = next;
        this.options = options.Value;
        this.metrics = metrics;
        permits = new SemaphoreSlim(
            this.options.GlobalConcurrencyLimit,
            this.options.GlobalConcurrencyLimit);
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!ShouldLimit(context) || await permits.WaitAsync(0, context.RequestAborted))
        {
            try
            {
                await next(context);
            }
            finally
            {
                if (ShouldLimit(context))
                {
                    permits.Release();
                }
            }

            return;
        }

        metrics.RecordRateLimitRejected("global");
        var retryAfterSeconds = Math.Clamp(
            options.DefaultRetryAfterSeconds,
            1,
            options.MaximumRetryAfterSeconds);
        context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.Response.ContentType = "application/problem+json";
        context.Response.Headers.RetryAfter = retryAfterSeconds.ToString(CultureInfo.InvariantCulture);
        context.Response.Headers["RateLimit-Limit"] = options.GlobalConcurrencyLimit
            .ToString(CultureInfo.InvariantCulture);
        context.Response.Headers["RateLimit-Remaining"] = "0";
        context.Response.Headers["RateLimit-Reset"] = retryAfterSeconds.ToString(CultureInfo.InvariantCulture);

        var problem = new ProblemDetails
        {
            Type = "https://veriscan.invalid/problems/rate-limit-exceeded",
            Title = "Too Many Requests",
            Status = StatusCodes.Status429TooManyRequests,
            Detail = "系统正在处理较多请求，请稍后重试。",
            Instance = context.Request.Path
        };
        problem.Extensions["code"] = "rate_limit_exceeded";
        problem.Extensions["scope"] = "global";
        problem.Extensions["retryAfterSeconds"] = retryAfterSeconds;
        await context.Response.WriteAsJsonAsync(
            problem,
            options: null,
            contentType: "application/problem+json",
            cancellationToken: context.RequestAborted);
    }

    public void Dispose()
    {
        permits.Dispose();
    }

    private bool ShouldLimit(HttpContext context)
    {
        return options.Enabled && context.Request.Path.StartsWithSegments(options.PathPrefix);
    }
}

public static class IngressConcurrencyApplicationBuilderExtensions
{
    public static IApplicationBuilder UseVeriScanIngressConcurrency(this IApplicationBuilder app)
    {
        return app.UseMiddleware<IngressConcurrencyMiddleware>();
    }
}
