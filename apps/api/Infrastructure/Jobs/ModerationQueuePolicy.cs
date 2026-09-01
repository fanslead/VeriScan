using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;
using VeriScan.Application.Abstractions;

namespace VeriScan.Infrastructure.Jobs;

public sealed class ModerationQueueOptions
{
    public const string SectionName = "Moderation:Queue";

    [Range(1, 100)]
    public int MaximumSyncItems { get; init; } = 100;

    [Range(1, 1000)]
    public int MaximumAsyncItems { get; init; } = 1000;

    [Range(1, 100)]
    public int AutoAsyncItemThreshold { get; init; } = 10;

    [Range(0, 100)]
    public int AutoAsyncAiThreshold { get; init; } = 4;

    [Range(1, 20)]
    public int MaximumAttempts { get; init; } = 4;

    [Range(10, 600)]
    public int LeaseSeconds { get; init; } = 90;

    [Range(100, 10000)]
    public int EmptyQueueDelayMilliseconds { get; init; } = 500;

    [Range(1, 3600)]
    public int MaximumRetryDelaySeconds { get; init; } = 300;
}

public sealed class ModerationQueuePolicy(IOptions<ModerationQueueOptions> options)
    : IModerationQueuePolicy
{
    private readonly ModerationQueueOptions _options = options.Value;

    public int MaximumSyncItems => _options.MaximumSyncItems;

    public int MaximumAsyncItems => _options.MaximumAsyncItems;

    public int AutoAsyncItemThreshold => _options.AutoAsyncItemThreshold;

    public int AutoAsyncAiThreshold => _options.AutoAsyncAiThreshold;

    public int MaximumAttempts => _options.MaximumAttempts;

    public TimeSpan LeaseDuration => TimeSpan.FromSeconds(_options.LeaseSeconds);

    public TimeSpan EmptyQueueDelay => TimeSpan.FromMilliseconds(_options.EmptyQueueDelayMilliseconds);

    public TimeSpan GetRetryDelay(int attemptCount)
    {
        var exponent = Math.Clamp(attemptCount - 1, 0, 10);
        var seconds = Math.Min(Math.Pow(2, exponent), _options.MaximumRetryDelaySeconds);
        return TimeSpan.FromSeconds(seconds);
    }
}
