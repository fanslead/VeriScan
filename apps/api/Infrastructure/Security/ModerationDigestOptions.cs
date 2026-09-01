using System.ComponentModel.DataAnnotations;

namespace VeriScan.Infrastructure.Security;

public sealed class ModerationDigestOptions
{
    public const string SectionName = "Security:ModerationDigests";

    [Required, MinLength(32)]
    public string ContentPepper { get; set; } = string.Empty;

    [Required, MinLength(32)]
    public string IdempotencyPepper { get; set; } = string.Empty;

    [Required, StringLength(32, MinimumLength = 1)]
    public string KeyVersion { get; set; } = "v1";
}
