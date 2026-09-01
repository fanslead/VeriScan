using System.Globalization;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using VeriScan.Api.Diagnostics;

namespace VeriScan.Api.RateLimiting;

public static class RateLimitingServiceCollectionExtensions
{
    public static IServiceCollection AddVeriScanRateLimiting(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var section = configuration.GetSection(RateLimitingOptions.SectionName);
        services.AddOptions<RateLimitingOptions>()
            .Bind(section)
            .ValidateDataAnnotations()
            .Validate(
                options => options.DefaultRetryAfterSeconds > 0,
                "RateLimiting:DefaultRetryAfterSeconds 必须是正数。")
            .Validate(
                options => options.MaximumRetryAfterSeconds >= options.DefaultRetryAfterSeconds,
                "RateLimiting:MaximumRetryAfterSeconds 不能小于 DefaultRetryAfterSeconds。")
            .ValidateOnStart();

        services.AddRateLimiter();
        services.AddSingleton<IConfigureOptions<Microsoft.AspNetCore.RateLimiting.RateLimiterOptions>, VeriScanRateLimiterOptionsSetup>();
        return services;
    }

    private sealed class VeriScanRateLimiterOptionsSetup(
        IOptions<RateLimitingOptions> configuredOptions) :
        IConfigureOptions<Microsoft.AspNetCore.RateLimiting.RateLimiterOptions>
    {
        public void Configure(Microsoft.AspNetCore.RateLimiting.RateLimiterOptions options)
        {
            var configured = configuredOptions.Value;
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.GlobalLimiter = BuildLimiter(configured);
            options.OnRejected = (context, cancellationToken) =>
                WriteRejectionAsync(context, configured, cancellationToken);
        }
    }

    private static PartitionedRateLimiter<HttpContext> BuildLimiter(RateLimitingOptions options)
    {
        var global = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            !ShouldLimit(context, options)
                ? RateLimitPartition.GetNoLimiter("global-disabled")
                : RateLimitPartition.GetTokenBucketLimiter(
                    "global",
                    _ => CreateTokenBucketOptions(
                        options.GlobalPermitLimit,
                        options.GlobalWindowSeconds,
                        options.GlobalQueueLimit)));
        var application = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        {
            if (!ShouldLimit(context, options) || !TryGetGuidClaim(context.User, "application_id", out var applicationId))
            {
                return RateLimitPartition.GetNoLimiter("application-disabled");
            }

            return RateLimitPartition.GetTokenBucketLimiter(
                $"application:{applicationId:N}",
                _ => CreateTokenBucketOptions(
                    options.ApplicationPermitLimit,
                    options.ApplicationWindowSeconds,
                    options.ApplicationQueueLimit));
        });
        var apiKey = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        {
            if (!ShouldLimit(context, options) || !TryGetGuidClaim(context.User, ClaimTypes.NameIdentifier, out var keyId))
            {
                return RateLimitPartition.GetNoLimiter("api-key-disabled");
            }

            return RateLimitPartition.GetTokenBucketLimiter(
                $"api-key:{keyId:N}",
                _ => CreateTokenBucketOptions(
                    options.ApiKeyPermitLimit,
                    options.ApiKeyWindowSeconds,
                    options.ApiKeyQueueLimit));
        });
        var applicationConcurrency = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        {
            if (!ShouldLimit(context, options) || !TryGetGuidClaim(context.User, "application_id", out var applicationId))
            {
                return RateLimitPartition.GetNoLimiter("application-concurrency-disabled");
            }

            return RateLimitPartition.GetConcurrencyLimiter(
                $"application-concurrency:{applicationId:N}",
                _ => CreateConcurrencyOptions(
                    options.ApplicationConcurrencyLimit,
                    options.ApplicationConcurrencyQueueLimit));
        });
        var apiKeyConcurrency = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        {
            if (!ShouldLimit(context, options) || !TryGetGuidClaim(context.User, ClaimTypes.NameIdentifier, out var keyId))
            {
                return RateLimitPartition.GetNoLimiter("api-key-concurrency-disabled");
            }

            return RateLimitPartition.GetConcurrencyLimiter(
                $"api-key-concurrency:{keyId:N}",
                _ => CreateConcurrencyOptions(
                    options.ApiKeyConcurrencyLimit,
                    options.ApiKeyConcurrencyQueueLimit));
        });

        return PartitionedRateLimiter.CreateChained(
            global,
            application,
            apiKey,
            applicationConcurrency,
            apiKeyConcurrency);
    }

    private static TokenBucketRateLimiterOptions CreateTokenBucketOptions(
        int permitLimit,
        int windowSeconds,
        int queueLimit)
    {
        return new TokenBucketRateLimiterOptions
        {
            TokenLimit = permitLimit,
            TokensPerPeriod = permitLimit,
            ReplenishmentPeriod = TimeSpan.FromSeconds(windowSeconds),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = queueLimit,
            AutoReplenishment = true
        };
    }

    private static ConcurrencyLimiterOptions CreateConcurrencyOptions(int permitLimit, int queueLimit)
    {
        return new ConcurrencyLimiterOptions
        {
            PermitLimit = permitLimit,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = queueLimit
        };
    }

    private static async ValueTask WriteRejectionAsync(
        OnRejectedContext context,
        RateLimitingOptions options,
        CancellationToken cancellationToken)
    {
        var httpContext = context.HttpContext;
        var retryAfterSeconds = GetRetryAfterSeconds(
            context.Lease,
            options.DefaultRetryAfterSeconds,
            options.MaximumRetryAfterSeconds);
        var scope = ResolveScope(httpContext.User);
        httpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        httpContext.Response.ContentType = "application/problem+json";
        httpContext.Response.Headers["Retry-After"] = retryAfterSeconds.ToString(CultureInfo.InvariantCulture);
        httpContext.Response.Headers["RateLimit-Limit"] = GetEffectiveLimit(httpContext.User, options)
            .ToString(CultureInfo.InvariantCulture);
        httpContext.Response.Headers["RateLimit-Remaining"] = "0";
        httpContext.Response.Headers["RateLimit-Reset"] = retryAfterSeconds.ToString(CultureInfo.InvariantCulture);
        httpContext.RequestServices
            .GetService<RequestMetrics>()?
            .RecordRateLimitRejected(scope);

        var problem = new ProblemDetails
        {
            Type = "https://veriscan.invalid/problems/rate-limit-exceeded",
            Title = "Too Many Requests",
            Status = StatusCodes.Status429TooManyRequests,
            Detail = "请求频率超过当前应用配额，请稍后重试。",
            Instance = httpContext.Request.Path
        };
        problem.Extensions["code"] = "rate_limit_exceeded";
        problem.Extensions["scope"] = scope;
        problem.Extensions["retryAfterSeconds"] = retryAfterSeconds;
        await httpContext.Response.WriteAsJsonAsync(
            problem,
            options: null,
            contentType: "application/problem+json",
            cancellationToken: cancellationToken);
    }

    private static int GetRetryAfterSeconds(
        RateLimitLease lease,
        int defaultSeconds,
        int maximumSeconds)
    {
        if (!lease.TryGetMetadata(MetadataName.RetryAfter, out TimeSpan retryAfter) || retryAfter <= TimeSpan.Zero)
        {
            return defaultSeconds;
        }

        var seconds = Math.Ceiling(retryAfter.TotalSeconds);
        return seconds >= maximumSeconds
            ? maximumSeconds
            : Math.Max(1, (int)seconds);
    }

    internal static int GetEffectiveLimit(ClaimsPrincipal principal, RateLimitingOptions options)
    {
        if (TryGetGuidClaim(principal, ClaimTypes.NameIdentifier, out _))
        {
            return Math.Min(options.GlobalPermitLimit, Math.Min(
                options.ApplicationPermitLimit,
                options.ApiKeyPermitLimit));
        }

        return TryGetGuidClaim(principal, "application_id", out _)
            ? Math.Min(options.GlobalPermitLimit, options.ApplicationPermitLimit)
            : options.GlobalPermitLimit;
    }

    internal static int GetEffectiveWindowSeconds(ClaimsPrincipal principal, RateLimitingOptions options)
    {
        if (TryGetGuidClaim(principal, ClaimTypes.NameIdentifier, out _))
        {
            return Math.Min(options.GlobalWindowSeconds, Math.Min(
                options.ApplicationWindowSeconds,
                options.ApiKeyWindowSeconds));
        }

        return TryGetGuidClaim(principal, "application_id", out _)
            ? Math.Min(options.GlobalWindowSeconds, options.ApplicationWindowSeconds)
            : options.GlobalWindowSeconds;
    }

    private static string ResolveScope(ClaimsPrincipal principal)
    {
        return TryGetGuidClaim(principal, ClaimTypes.NameIdentifier, out _)
            ? "api_key"
            : TryGetGuidClaim(principal, "application_id", out _)
                ? "application"
                : "global";
    }

    private static bool TryGetGuidClaim(
        ClaimsPrincipal principal,
        string claimType,
        out Guid value)
    {
        return Guid.TryParse(principal.FindFirst(claimType)?.Value, out value);
    }

    private static bool ShouldLimit(HttpContext context, RateLimitingOptions options)
    {
        return options.Enabled && context.Request.Path.StartsWithSegments(options.PathPrefix);
    }
}
