using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using VeriScan.Api.Diagnostics;

namespace VeriScan.Api.Tests;

public sealed class RequestObservabilityTests
{
    [Fact]
    public async Task LogsRouteStatusAndTraceWithoutApiKeyOrBody()
    {
        var logger = new RecordingLogger();
        var context = CreateContext("POST", "/api/v1/moderation/batches", "/api/v1/moderation/batches");
        context.Request.Headers["X-API-Key"] = "vk_live_should-never-be-logged";
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("content that must never be logged"));
        context.Response.StatusCode = StatusCodes.Status202Accepted;
        var metrics = CreateMetrics();
        var middleware = CreateMiddleware(logger, metrics, excludeHealthChecks: true, _ => Task.CompletedTask);

        using var activity = new Activity("request-test").Start();
        await middleware.InvokeAsync(context);

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Equal("/api/v1/moderation/batches", entry.Properties["Route"]);
        Assert.Equal(StatusCodes.Status202Accepted, entry.Properties["StatusCode"]);
        Assert.Equal("POST", entry.Properties["HttpMethod"]);
        Assert.Equal(activity.TraceId.ToHexString(), entry.Properties["TraceId"]);
        Assert.DoesNotContain("vk_live_should-never-be-logged", entry.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("content that must never be logged", entry.Message, StringComparison.Ordinal);
        Assert.Equal(
            "/api/v1/moderation/batches",
            activity.GetTagItem("http.route"));
        Assert.Equal(
            StatusCodes.Status202Accepted,
            activity.GetTagItem("http.response.status_code"));
    }

    [Fact]
    public async Task ExcludesConfiguredHealthEndpoint()
    {
        var logger = new RecordingLogger();
        var context = CreateContext("GET", "/healthz", "/healthz");
        var metrics = CreateMetrics();
        var middleware = CreateMiddleware(logger, metrics, excludeHealthChecks: true, _ =>
        {
            context.Response.StatusCode = StatusCodes.Status200OK;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        Assert.Empty(logger.Entries);
    }

    [Fact]
    public async Task RecordsServerErrorWhenDownstreamThrows()
    {
        var logger = new RecordingLogger();
        var context = CreateContext("GET", "/api/v1/failure", "/api/v1/failure");
        var metrics = CreateMetrics();
        var middleware = CreateMiddleware(logger, metrics, excludeHealthChecks: true, _ =>
            throw new InvalidOperationException("only the test exception"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => middleware.InvokeAsync(context));

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(StatusCodes.Status500InternalServerError, entry.Properties["StatusCode"]);
        Assert.DoesNotContain("only the test exception", entry.Message, StringComparison.Ordinal);
    }

    private static RequestTelemetryMiddleware CreateMiddleware(
        RecordingLogger logger,
        RequestMetrics metrics,
        bool excludeHealthChecks,
        RequestDelegate next)
    {
        return new RequestTelemetryMiddleware(
            next,
            logger,
            metrics,
            new StaticOptionsMonitor<ObservabilityOptions>(new ObservabilityOptions
            {
                ExcludeHealthChecks = excludeHealthChecks,
                HealthPath = "/healthz"
            }));
    }

    private static DefaultHttpContext CreateContext(string method, string path, string route)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;
        context.SetEndpoint(new RouteEndpoint(
            _ => Task.CompletedTask,
            RoutePatternFactory.Parse(route),
            0,
            EndpointMetadataCollection.Empty,
            route));
        return context;
    }

    private static RequestMetrics CreateMetrics()
    {
        var services = new ServiceCollection()
            .AddMetrics()
            .AddSingleton<RequestMetrics>()
            .BuildServiceProvider();
        return services.GetRequiredService<RequestMetrics>();
    }

    private sealed class StaticOptionsMonitor<T>(T currentValue) : IOptionsMonitor<T>
        where T : class
    {
        public T CurrentValue { get; } = currentValue;

        public T Get(string? name) => CurrentValue;

        public IDisposable OnChange(Action<T, string?> listener) => EmptyDisposable.Instance;
    }

    private sealed class EmptyDisposable : IDisposable
    {
        public static EmptyDisposable Instance { get; } = new();

        public void Dispose()
        {
        }
    }

    private sealed class RecordingLogger : ILogger<RequestTelemetryMiddleware>
    {
        public ConcurrentBag<LogEntry> Entries { get; } = new();

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => EmptyDisposable.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var properties = state is IEnumerable<KeyValuePair<string, object?>> values
                ? values.ToDictionary(pair => pair.Key, pair => pair.Value)
                : new Dictionary<string, object?>();
            Entries.Add(new LogEntry(logLevel, formatter(state, exception), properties));
        }
    }

    private sealed record LogEntry(
        LogLevel Level,
        string Message,
        IReadOnlyDictionary<string, object?> Properties);
}
