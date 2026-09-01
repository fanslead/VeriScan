using Microsoft.Extensions.Configuration;
using VeriScan.Application.Abstractions;

namespace VeriScan.Infrastructure.ExternalAi;

public sealed class ModerationExecutionPolicy(IConfiguration configuration)
    : IModerationExecutionPolicy
{
    public int MaximumConcurrentAiCalls { get; } = Math.Clamp(
        configuration.GetValue("ExternalAi:MaximumConcurrentCallsPerBatch", 4),
        1,
        16);

    public TimeSpan SynchronousDeadline { get; } = TimeSpan.FromMilliseconds(Math.Clamp(
        configuration.GetValue("ExternalAi:MaximumSynchronousBatchMs", 30_000),
        1_000,
        120_000));
}
