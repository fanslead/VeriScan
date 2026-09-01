using System.ComponentModel.DataAnnotations;

namespace VeriScan.Infrastructure.Security;

public sealed class ApiKeyOptions
{
    public const string SectionName = "Security:ApiKey";

    [Required, MinLength(32)]
    public string Pepper { get; set; } = string.Empty;

    [Required, StringLength(32, MinimumLength = 1)]
    public string PepperVersion { get; set; } = "v1";

    [Range(1, 100)]
    public int MaximumActiveKeys { get; set; } = 5;

    [Range(1, 3650)]
    public int MaximumLifetimeDays { get; set; } = 365;
}
