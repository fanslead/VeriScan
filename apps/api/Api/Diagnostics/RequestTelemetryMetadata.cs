using Microsoft.AspNetCore.Routing;

namespace VeriScan.Api.Diagnostics;

internal static class RequestTelemetryMetadata
{
    public const string UnmatchedRoute = "unmatched";

    public static bool IsExcluded(HttpContext context, ObservabilityOptions options)
    {
        if (!options.ExcludeHealthChecks)
        {
            return false;
        }

        var healthPath = string.IsNullOrWhiteSpace(options.HealthPath)
            ? "/healthz"
            : options.HealthPath.Trim();
        if (!healthPath.StartsWith('/'))
        {
            healthPath = $"/{healthPath}";
        }

        return context.Request.Path.StartsWithSegments(new PathString(healthPath));
    }

    public static string ResolveRoute(HttpContext context)
    {
        return context.GetEndpoint() is RouteEndpoint routeEndpoint
            ? routeEndpoint.RoutePattern.RawText ?? UnmatchedRoute
            : UnmatchedRoute;
    }
}
