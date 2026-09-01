using VeriScan.Application.Abstractions;
using VeriScan.Domain.Entities;

namespace VeriScan.Api.Tests;

internal sealed class TestAiEndpointPolicy : IAiEndpointPolicy
{
    public void Validate(Uri endpoint)
    {
        if (endpoint.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException("Test endpoints must use HTTPS.");
        }
    }
}

internal sealed class TestAiConfigurationProbe : IAiConfigurationProbe
{
    public bool Succeeded { get; set; } = true;

    public string? FailureCode { get; set; }

    public Task<AiConfigurationProbeResult> ProbeAsync(
        AiModelConfiguration configuration,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(new AiConfigurationProbeResult(
            Succeeded,
            configuration.Protocol.ToString(),
            configuration.Model,
            12,
            18,
            22,
            FailureCode));
    }
}

internal sealed class TestModerationAiClient : IModerationAiClient
{
    private int calls;
    private int activeCalls;
    private int maximumConcurrentCalls;

    public AiModerationResult Result { get; set; } = NoActiveConfiguration();

    public TimeSpan Delay { get; set; }

    public int Calls => Volatile.Read(ref calls);

    public int MaximumConcurrentCalls => Volatile.Read(ref maximumConcurrentCalls);

    public async Task<AiModerationResult> ModerateAsync(
        AiModerationRequest request,
        CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref calls);
        var active = Interlocked.Increment(ref activeCalls);
        UpdateMaximum(active);
        try
        {
            if (Delay > TimeSpan.Zero)
            {
                await Task.Delay(Delay, cancellationToken);
            }

            return Result;
        }
        finally
        {
            Interlocked.Decrement(ref activeCalls);
        }
    }

    private void UpdateMaximum(int candidate)
    {
        var current = Volatile.Read(ref maximumConcurrentCalls);
        while (candidate > current)
        {
            var observed = Interlocked.CompareExchange(ref maximumConcurrentCalls, candidate, current);
            if (observed == current)
            {
                return;
            }

            current = observed;
        }
    }

    private static AiModerationResult NoActiveConfiguration()
    {
        return new AiModerationResult(
            AiModerationOutcome.NoActiveConfiguration,
            null,
            [],
            [],
            [],
            null,
            null,
            null,
            null,
            "AI_ROUTE_NOT_CONFIGURED");
    }
}

internal sealed class TestAiSchemaDescriptor : IAiSchemaDescriptor
{
    public AiSchemaDescriptor Describe(AiProtocol protocol)
    {
        return new AiSchemaDescriptor(
            "test-adapter@1",
            "moderation-output@1",
            new string('a', 64),
            new string('b', 64),
            "test-transformer@1");
    }
}
