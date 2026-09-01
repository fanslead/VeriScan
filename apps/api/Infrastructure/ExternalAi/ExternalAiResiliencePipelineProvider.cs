using System.Collections.Concurrent;
using System.Net;
using Microsoft.Extensions.Http.Resilience;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Timeout;
using VeriScan.Domain.Entities;

namespace VeriScan.Infrastructure.ExternalAi;

internal sealed class ExternalAiResiliencePipelineProvider
{
    private readonly ConcurrentDictionary<PipelineKey, Lazy<ResiliencePipeline<HttpResponseMessage>>> pipelines = [];

    public ResiliencePipeline<HttpResponseMessage> GetPipeline(
        AiModelConfiguration configuration,
        ExternalAiOptions options)
    {
        var attempts = Math.Clamp(configuration.MaxAttempts, 1, options.MaximumAttempts);
        var timeoutMs = Math.Clamp(
            configuration.RequestTimeoutMs,
            1,
            options.MaximumRequestTimeoutMs);
        var key = new PipelineKey(
            configuration.PublicRevisionId,
            configuration.Protocol,
            attempts,
            timeoutMs,
            options.RetryBaseDelayMs,
            options.RetryMaximumDelayMs,
            options.RetryUseJitter,
            options.CircuitFailureRatio,
            options.CircuitMinimumThroughput,
            options.CircuitSamplingDurationSeconds,
            options.CircuitBreakDurationSeconds);

        return pipelines.GetOrAdd(
                key,
                static (pipelineKey, state) => new Lazy<ResiliencePipeline<HttpResponseMessage>>(
                    () => BuildPipeline(pipelineKey, state.options, state.protocol),
                    LazyThreadSafetyMode.ExecutionAndPublication),
                (options: options, protocol: configuration.Protocol))
            .Value;
    }

    private static ResiliencePipeline<HttpResponseMessage> BuildPipeline(
        PipelineKey key,
        ExternalAiOptions options,
        AiProtocol protocol)
    {
        var pipelineBuilder = new ResiliencePipelineBuilder<HttpResponseMessage>();
        if (key.Attempts > 1)
        {
            var retryOptions = new HttpRetryStrategyOptions
            {
                MaxRetryAttempts = key.Attempts - 1,
                BackoffType = DelayBackoffType.Exponential,
                Delay = TimeSpan.FromMilliseconds(options.RetryBaseDelayMs),
                MaxDelay = TimeSpan.FromMilliseconds(options.RetryMaximumDelayMs),
                UseJitter = options.RetryUseJitter,
                ShouldRetryAfterHeader = false,
                DelayGenerator = arguments => ValueTask.FromResult(
                    GetRetryAfterDelay(arguments.Outcome.Result, options.RetryMaximumDelayMs)),
                ShouldHandle = CreateTransientPredicate(),
                OnRetry = arguments =>
                {
                    var statusCode = arguments.Outcome.Result?.StatusCode;
                    ExternalAiMetrics.RecordRetry(protocol.ToString(), statusCode is { } value ? (int)value : 0);
                    return ValueTask.CompletedTask;
                }
            };
            pipelineBuilder.AddRetry(retryOptions);
        }

        var circuitOptions = new HttpCircuitBreakerStrategyOptions
        {
            FailureRatio = options.CircuitFailureRatio,
            MinimumThroughput = options.CircuitMinimumThroughput,
            SamplingDuration = TimeSpan.FromSeconds(options.CircuitSamplingDurationSeconds),
            BreakDuration = TimeSpan.FromSeconds(options.CircuitBreakDurationSeconds),
            ShouldHandle = CreateTransientPredicate(),
            OnOpened = _ =>
            {
                ExternalAiMetrics.RecordCircuitOpened(protocol.ToString());
                return ValueTask.CompletedTask;
            }
        };
        var timeoutOptions = new HttpTimeoutStrategyOptions
        {
            Timeout = TimeSpan.FromMilliseconds(key.TimeoutMs)
        };

        return pipelineBuilder
            .AddCircuitBreaker(circuitOptions)
            .AddTimeout(timeoutOptions)
            .Build();
    }

    private static PredicateBuilder<HttpResponseMessage> CreateTransientPredicate()
    {
        return new PredicateBuilder<HttpResponseMessage>()
            .Handle<HttpRequestException>()
            .Handle<TimeoutRejectedException>()
            .HandleResult(static response => response.StatusCode == HttpStatusCode.RequestTimeout ||
                response.StatusCode == HttpStatusCode.TooManyRequests ||
                (int)response.StatusCode >= 500);
    }

    private static TimeSpan? GetRetryAfterDelay(HttpResponseMessage? response, int maximumDelayMs)
    {
        if (response?.Headers.RetryAfter is not { } retryAfter)
        {
            return null;
        }

        var delay = retryAfter.Delta;
        if (delay is null && retryAfter.Date is { } retryAt)
        {
            delay = retryAt - DateTimeOffset.UtcNow;
        }

        return delay is { } retryDelay && retryDelay > TimeSpan.Zero
            ? TimeSpan.FromMilliseconds(Math.Min(maximumDelayMs, retryDelay.TotalMilliseconds))
            : null;
    }

    private readonly record struct PipelineKey(
        string ConfigurationRevision,
        AiProtocol Protocol,
        int Attempts,
        int TimeoutMs,
        int RetryBaseDelayMs,
        int RetryMaximumDelayMs,
        bool RetryUseJitter,
        double CircuitFailureRatio,
        int CircuitMinimumThroughput,
        int CircuitSamplingDurationSeconds,
        int CircuitBreakDurationSeconds);
}
