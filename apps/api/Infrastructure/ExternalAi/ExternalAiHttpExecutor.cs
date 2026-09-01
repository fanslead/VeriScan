using System.Buffers;
using System.Diagnostics;
using System.Net;
using System.Text;
using Microsoft.Extensions.Options;
using Polly.CircuitBreaker;
using Polly.Timeout;
using VeriScan.Domain.Entities;

namespace VeriScan.Infrastructure.ExternalAi;

internal sealed record ExternalAiHttpResult(
    HttpStatusCode? StatusCode,
    string? Body,
    string? ProviderRequestId,
    string? FailureCode);

public sealed class ExternalAiHttpExecutor
{
    private readonly IOptionsMonitor<ExternalAiOptions> options;
    private readonly ExternalAiResiliencePipelineProvider pipelineProvider;

    public ExternalAiHttpExecutor(IOptionsMonitor<ExternalAiOptions> options)
    {
        this.options = options;
        pipelineProvider = new ExternalAiResiliencePipelineProvider();
    }

    internal ExternalAiHttpExecutor(
        IOptionsMonitor<ExternalAiOptions> options,
        ExternalAiResiliencePipelineProvider pipelineProvider)
    {
        this.options = options;
        this.pipelineProvider = pipelineProvider;
    }

    internal async Task<ExternalAiHttpResult> ExecuteAsync(
        HttpClient client,
        AiModelConfiguration configuration,
        Func<HttpRequestMessage> requestFactory,
        CancellationToken cancellationToken)
    {
        var currentOptions = options.CurrentValue;
        using var timeoutCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCancellation.CancelAfter(TimeSpan.FromMilliseconds(Math.Clamp(
            configuration.RequestTimeoutMs,
            1,
            currentOptions.MaximumRequestTimeoutMs)));

        var protocol = configuration.Protocol.ToString();
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var pipeline = pipelineProvider.GetPipeline(configuration, currentOptions);
            using var response = await pipeline.ExecuteAsync(
                async pipelineCancellationToken =>
                {
                    using var request = requestFactory();
                    return await client.SendAsync(
                        request,
                        HttpCompletionOption.ResponseHeadersRead,
                        pipelineCancellationToken);
                },
                timeoutCancellation.Token);
            var body = await ReadBodyAsync(response, timeoutCancellation.Token);
            var result = new ExternalAiHttpResult(
                response.StatusCode,
                body,
                GetProviderRequestId(response),
                null);
            ExternalAiMetrics.RecordRequest(protocol, body is null ? "response_too_large" : MapResponseOutcome(response.StatusCode));
            return result;
        }
        catch (ExternalAiNetworkPolicyException)
        {
            ExternalAiMetrics.RecordRequest(protocol, "network_policy_denied");
            return new ExternalAiHttpResult(null, null, null, "AI_NETWORK_POLICY_DENIED");
        }
        catch (BrokenCircuitException)
        {
            ExternalAiMetrics.RecordRequest(protocol, "circuit_open");
            return new ExternalAiHttpResult(null, null, null, "AI_CIRCUIT_OPEN");
        }
        catch (TimeoutRejectedException)
        {
            ExternalAiMetrics.RecordTimeout(protocol);
            ExternalAiMetrics.RecordRequest(protocol, "timeout");
            return new ExternalAiHttpResult(null, null, null, "AI_TIMEOUT");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            ExternalAiMetrics.RecordTimeout(protocol);
            ExternalAiMetrics.RecordRequest(protocol, "timeout");
            return new ExternalAiHttpResult(null, null, null, "AI_TIMEOUT");
        }
        catch (HttpRequestException)
        {
            ExternalAiMetrics.RecordRequest(protocol, "network_error");
            return new ExternalAiHttpResult(null, null, null, "AI_NETWORK_ERROR");
        }
        finally
        {
            stopwatch.Stop();
            ExternalAiMetrics.RecordDuration(protocol, stopwatch.Elapsed.TotalMilliseconds);
        }
    }

    private async Task<string?> ReadBodyAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var maximumBytes = options.CurrentValue.MaximumResponseBytes;
        if (response.Content.Headers.ContentLength is > 0 and var contentLength && contentLength > maximumBytes)
        {
            return null;
        }

        var maximumBytesWithProbe = checked(maximumBytes + 1);
        var buffer = ArrayPool<byte>.Shared.Rent(maximumBytesWithProbe);
        try
        {
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var length = 0;
            while (length < maximumBytesWithProbe)
            {
                var read = await stream.ReadAsync(
                    buffer.AsMemory(length, maximumBytesWithProbe - length),
                    cancellationToken);
                if (read == 0)
                {
                    return Encoding.UTF8.GetString(buffer, 0, length);
                }

                length += read;
            }

            return null;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static string? GetProviderRequestId(HttpResponseMessage response)
    {
        return response.Headers.TryGetValues("x-request-id", out var xRequestIds)
            ? xRequestIds.FirstOrDefault()
            : response.Headers.TryGetValues("request-id", out var requestIds)
                ? requestIds.FirstOrDefault()
                : null;
    }

    private static string MapResponseOutcome(HttpStatusCode statusCode)
    {
        return statusCode switch
        {
            >= HttpStatusCode.OK and < HttpStatusCode.MultipleChoices => "success",
            HttpStatusCode.TooManyRequests => "rate_limited",
            >= HttpStatusCode.InternalServerError => "provider_error",
            _ => "provider_rejected"
        };
    }
}
