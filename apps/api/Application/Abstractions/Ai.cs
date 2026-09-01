using VeriScan.Domain.Entities;

namespace VeriScan.Application.Abstractions;

public interface IAiModelConfigurationStore
{
    Task AddAsync(AiModelConfiguration configuration, CancellationToken cancellationToken);

    Task<AiModelConfiguration?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<AiModelConfiguration?> GetActiveAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<AiModelConfiguration>> ListAsync(CancellationToken cancellationToken);

    Task ActivateExclusiveAsync(
        AiModelConfiguration configuration,
        DateTimeOffset activatedAt,
        CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public interface IAiEndpointPolicy
{
    void Validate(Uri endpoint);
}

public interface IModerationAiClient
{
    Task<AiModerationResult> ModerateAsync(
        AiModerationRequest request,
        CancellationToken cancellationToken);
}

public interface IAiConfigurationProbe
{
    Task<AiConfigurationProbeResult> ProbeAsync(
        AiModelConfiguration configuration,
        CancellationToken cancellationToken);
}

public interface IAiSchemaDescriptor
{
    AiSchemaDescriptor Describe(AiProtocol protocol);
}

public sealed record AiModerationRequest(
    Guid TenantId,
    Guid ApplicationId,
    string Content,
    string? Language);

public sealed record AiModerationResult(
    AiModerationOutcome Outcome,
    AiModerationLabel? Label,
    IReadOnlyList<string> ReasonCodes,
    IReadOnlyList<AiModerationCategory> Categories,
    IReadOnlyList<string> Evidence,
    string? ConfigurationRevision,
    string? ProviderRequestId,
    int? InputTokens,
    int? OutputTokens,
    string? FailureCode);

public sealed record AiModerationCategory(string Code, AiCategorySeverity Severity);

public sealed record AiConfigurationProbeResult(
    bool Succeeded,
    string Protocol,
    string Model,
    long LatencyMs,
    int? InputTokens,
    int? OutputTokens,
    string? FailureCode);

public sealed record AiSchemaDescriptor(
    string AdapterContractVersion,
    string CanonicalSchemaVersion,
    string CanonicalSchemaHash,
    string EffectiveSchemaHash,
    string SchemaTransformerVersion);

public enum AiModerationOutcome
{
    Succeeded,
    NoActiveConfiguration,
    ProviderRefusal,
    Truncated,
    InvalidOutput,
    Unavailable,
    PolicyDenied
}

public enum AiModerationLabel
{
    Safe,
    Unsafe,
    Review
}

public enum AiCategorySeverity
{
    Low,
    Medium,
    High
}
