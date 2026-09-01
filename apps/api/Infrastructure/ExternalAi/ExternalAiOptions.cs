using System.ComponentModel.DataAnnotations;

namespace VeriScan.Infrastructure.ExternalAi;

public sealed class ExternalAiOptions
{
    public const string SectionName = "ExternalAi";

    public string[] AllowedHosts { get; set; } = [];

    public int[] AllowedPorts { get; set; } = [443];

    [Range(100, 30_000)]
    public int ConnectTimeoutMs { get; set; } = 30_000;

    [Range(16_384, 4 * 1024 * 1024)]
    public int MaximumResponseBytes { get; set; } = 1_048_576;
}
