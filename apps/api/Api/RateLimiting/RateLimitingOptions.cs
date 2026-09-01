using System.ComponentModel.DataAnnotations;

namespace VeriScan.Api.RateLimiting;

public sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    public bool Enabled { get; set; } = true;

    [Required, MinLength(1)]
    public string PathPrefix { get; set; } = "/api/v1/moderation";

    [Range(1, 1_000_000)]
    public int GlobalPermitLimit { get; set; } = 2_000;

    [Range(1, 3_600)]
    public int GlobalWindowSeconds { get; set; } = 1;

    [Range(0, 100_000)]
    public int GlobalQueueLimit { get; set; }

    [Range(1, 100_000)]
    public int GlobalConcurrencyLimit { get; set; } = 64;

    [Range(1, 1_000_000)]
    public int ApplicationPermitLimit { get; set; } = 500;

    [Range(1, 3_600)]
    public int ApplicationWindowSeconds { get; set; } = 1;

    [Range(0, 100_000)]
    public int ApplicationQueueLimit { get; set; } = 16;

    [Range(1, 1_000_000)]
    public int ApiKeyPermitLimit { get; set; } = 120;

    [Range(1, 3_600)]
    public int ApiKeyWindowSeconds { get; set; } = 1;

    [Range(0, 100_000)]
    public int ApiKeyQueueLimit { get; set; } = 8;

    [Range(1, 100_000)]
    public int ApplicationConcurrencyLimit { get; set; } = 64;

    [Range(0, 100_000)]
    public int ApplicationConcurrencyQueueLimit { get; set; } = 16;

    [Range(1, 100_000)]
    public int ApiKeyConcurrencyLimit { get; set; } = 16;

    [Range(0, 100_000)]
    public int ApiKeyConcurrencyQueueLimit { get; set; } = 4;

    [Range(1, 3_600)]
    public int DefaultRetryAfterSeconds { get; set; } = 1;

    [Range(1, 86_400)]
    public int MaximumRetryAfterSeconds { get; set; } = 60;
}
