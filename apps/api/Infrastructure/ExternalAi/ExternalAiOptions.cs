using System.ComponentModel.DataAnnotations;

namespace VeriScan.Infrastructure.ExternalAi;

public sealed class ExternalAiOptions : IValidatableObject
{
    public const string SectionName = "ExternalAi";

    public string[] AllowedHosts { get; set; } = [];

    public int[] AllowedPorts { get; set; } = [443];

    [Range(100, 30_000)]
    public int ConnectTimeoutMs { get; set; } = 30_000;

    [Range(16_384, 4 * 1024 * 1024)]
    public int MaximumResponseBytes { get; set; } = 1_048_576;

    [Range(500, 120_000)]
    public int MaximumRequestTimeoutMs { get; set; } = 120_000;

    [Range(1, 3)]
    public int MaximumAttempts { get; set; } = 3;

    [Range(10, 10_000)]
    public int RetryBaseDelayMs { get; set; } = 100;

    [Range(10, 30_000)]
    public int RetryMaximumDelayMs { get; set; } = 5_000;

    public bool RetryUseJitter { get; set; } = true;

    [Range(0.01, 1.0)]
    public double CircuitFailureRatio { get; set; } = 0.5;

    [Range(2, 10_000)]
    public int CircuitMinimumThroughput { get; set; } = 20;

    [Range(1, 3_600)]
    public int CircuitSamplingDurationSeconds { get; set; } = 30;

    [Range(1, 3_600)]
    public int CircuitBreakDurationSeconds { get; set; } = 30;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (RetryMaximumDelayMs < RetryBaseDelayMs)
        {
            yield return new ValidationResult(
                "RetryMaximumDelayMs 不能小于 RetryBaseDelayMs。",
                [nameof(RetryMaximumDelayMs)]);
        }

        if (MaximumRequestTimeoutMs <= ConnectTimeoutMs)
        {
            yield return new ValidationResult(
                "MaximumRequestTimeoutMs 必须大于 ConnectTimeoutMs。",
                [nameof(MaximumRequestTimeoutMs)]);
        }
    }
}
