using System.Buffers;
using System.Net;
using System.Text;
using Microsoft.Extensions.Options;
using VeriScan.Domain.Entities;

namespace VeriScan.Infrastructure.ExternalAi;

internal sealed record ExternalAiHttpResult(
    HttpStatusCode? StatusCode,
    string? Body,
    string? ProviderRequestId,
    string? FailureCode);

public sealed class ExternalAiHttpExecutor(IOptionsMonitor<ExternalAiOptions> options)
{
    internal async Task<ExternalAiHttpResult> ExecuteAsync(
        HttpClient client,
        AiModelConfiguration configuration,
        Func<HttpRequestMessage> requestFactory,
        CancellationToken cancellationToken)
    {
        using var timeoutCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCancellation.CancelAfter(TimeSpan.FromMilliseconds(Math.Max(1, configuration.RequestTimeoutMs)));
        var attempts = Math.Clamp(configuration.MaxAttempts, 1, 3);

        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            try
            {
                using var request = requestFactory();
                using var response = await client.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    timeoutCancellation.Token);
                var body = await ReadBodyAsync(response, timeoutCancellation.Token);
                var providerRequestId = GetProviderRequestId(response);
                if (IsRetryable(response.StatusCode) && attempt < attempts)
                {
                    await DelayBeforeRetryAsync(response, attempt, timeoutCancellation.Token);
                    continue;
                }

                return new ExternalAiHttpResult(response.StatusCode, body, providerRequestId, null);
            }
            catch (ExternalAiNetworkPolicyException)
            {
                return new ExternalAiHttpResult(null, null, null, "AI_NETWORK_POLICY_DENIED");
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                if (attempt < attempts)
                {
                    await DelayBeforeRetryAsync(null, attempt, timeoutCancellation.Token);
                    continue;
                }

                return new ExternalAiHttpResult(null, null, null, "AI_TIMEOUT");
            }
            catch (HttpRequestException)
            {
                if (attempt < attempts)
                {
                    await DelayBeforeRetryAsync(null, attempt, timeoutCancellation.Token);
                    continue;
                }

                return new ExternalAiHttpResult(null, null, null, "AI_NETWORK_ERROR");
            }
        }

        return new ExternalAiHttpResult(null, null, null, "AI_NETWORK_ERROR");
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

        var maximumBytesWithProbe = maximumBytes + 1;
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

    private static bool IsRetryable(HttpStatusCode statusCode)
    {
        return statusCode == HttpStatusCode.RequestTimeout ||
               statusCode == HttpStatusCode.TooManyRequests ||
               (int)statusCode >= 500;
    }

    private static Task DelayBeforeRetryAsync(
        HttpResponseMessage? response,
        int attempt,
        CancellationToken cancellationToken)
    {
        var delay = response?.Headers.RetryAfter?.Delta;
        if (delay is null && response?.Headers.RetryAfter?.Date is { } retryAt)
        {
            delay = retryAt - DateTimeOffset.UtcNow;
        }

        var delayMs = delay is { } retryDelay && retryDelay > TimeSpan.Zero
            ? Math.Min(5_000, (int)Math.Min(int.MaxValue, retryDelay.TotalMilliseconds))
            : Math.Min(1_000, 100 * (1 << Math.Min(attempt - 1, 3)));
        return Task.Delay(delayMs, cancellationToken);
    }
}
