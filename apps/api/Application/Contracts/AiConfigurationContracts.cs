using System.ComponentModel.DataAnnotations;
using VeriScan.Domain.Entities;

namespace VeriScan.Application.Contracts;

public sealed record CreateAiConfigurationRequest : AiConfigurationDraftRequest;

public record AiConfigurationDraftRequest
{
    [Required, StringLength(100, MinimumLength = 2)]
    public required string Name { get; init; }

    public required AiProtocol Protocol { get; init; }

    [Required, StringLength(2048)]
    public required string BaseUrl { get; init; }

    [Required, StringLength(256)]
    public required string EndpointPath { get; init; }

    [Required, StringLength(256)]
    public required string CredentialRef { get; init; }

    public required AiAuthScheme AuthScheme { get; init; }

    [Required, StringLength(200)]
    public required string Model { get; init; }

    [StringLength(64)]
    public string? ApiVersion { get; init; }

    public AiApiVersionLocation ApiVersionLocation { get; init; }

    [Required, StringLength(12000, MinimumLength = 20)]
    public required string SystemPrompt { get; init; }

    public AiDecodingMode DecodingMode { get; init; } = AiDecodingMode.OmitTemperature;

    [Range(128, 1_000_000)]
    public int MaxInputTokens { get; init; } = 4096;

    [Range(32, 32_768)]
    public int MaxOutputTokens { get; init; } = 512;

    [Range(100, 30_000)]
    public int ConnectTimeoutMs { get; init; } = 2000;

    [Range(500, 120_000)]
    public int RequestTimeoutMs { get; init; } = 15_000;

    [Range(1, 3)]
    public int MaxAttempts { get; init; } = 2;

    [Required, StringLength(100)]
    public required string DataRegion { get; init; }

    [Required, StringLength(100)]
    public required string RetentionClass { get; init; }
}

public sealed record AiConfigurationResponse(
    Guid Id,
    string PublicRevisionId,
    string Name,
    AiProtocol Protocol,
    string BaseUrl,
    string EndpointPath,
    string CredentialRef,
    AiAuthScheme AuthScheme,
    string Model,
    string? ApiVersion,
    AiApiVersionLocation ApiVersionLocation,
    string SystemPrompt,
    AiDecodingMode DecodingMode,
    int MaxInputTokens,
    int MaxOutputTokens,
    int ConnectTimeoutMs,
    int RequestTimeoutMs,
    int MaxAttempts,
    string DataRegion,
    string RetentionClass,
    AiConfigurationStatus Status,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? PublishedAt,
    DateTimeOffset? LastTestedAt,
    bool? LastTestSucceeded,
    string? LastTestFailureCode,
    string? AdapterContractVersion,
    string? CanonicalSchemaVersion,
    string? CanonicalSchemaHash,
    string? EffectiveSchemaHash,
    string? SchemaTransformerVersion);

public sealed record AiConfigurationListResponse(IReadOnlyList<AiConfigurationResponse> Items);

public sealed record AiConfigurationTestResponse(
    bool Succeeded,
    string Protocol,
    string Model,
    long LatencyMs,
    int? InputTokens,
    int? OutputTokens,
    string? FailureCode);
