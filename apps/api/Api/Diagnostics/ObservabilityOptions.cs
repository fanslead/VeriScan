namespace VeriScan.Api.Diagnostics;

public sealed class ObservabilityOptions
{
    public const string SectionName = "Observability";

    public bool ExcludeHealthChecks { get; set; } = true;

    public string HealthPath { get; set; } = "/healthz";

    public OtlpOptions Otlp { get; set; } = new();
}

public sealed class OtlpOptions
{
    public bool Enabled { get; set; }

    public string? Endpoint { get; set; }

    public string Protocol { get; set; } = "grpc";
}
