using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;
using VeriScan.Application.Abstractions;

namespace VeriScan.Infrastructure.Jobs;

public sealed class ModerationIdempotencyOptions
{
    public const string SectionName = "Moderation:Idempotency";

    [Range(1, 8760)]
    public int OperationRetentionHours { get; init; } = 24;
}

public sealed class ModerationIdempotencyPolicy(IOptions<ModerationIdempotencyOptions> options)
    : IModerationIdempotencyPolicy
{
    public TimeSpan OperationRetention => TimeSpan.FromHours(options.Value.OperationRetentionHours);
}
