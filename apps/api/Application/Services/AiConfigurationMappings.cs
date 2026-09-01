using VeriScan.Application.Contracts;
using VeriScan.Domain.Entities;

namespace VeriScan.Application.Services;

internal static class AiConfigurationMappings
{
    public static AiConfigurationResponse ToResponse(AiModelConfiguration configuration)
    {
        return new AiConfigurationResponse(
            configuration.Id,
            configuration.PublicRevisionId,
            configuration.Name,
            configuration.Protocol,
            configuration.BaseUrl,
            configuration.EndpointPath,
            configuration.CredentialRef,
            !string.IsNullOrWhiteSpace(configuration.CredentialCiphertext) ||
            configuration.CredentialRef.StartsWith("config://", StringComparison.Ordinal),
            string.IsNullOrWhiteSpace(configuration.CredentialCiphertext) ? "server" : "managed",
            configuration.AuthScheme,
            configuration.Model,
            configuration.ApiVersion,
            configuration.ApiVersionLocation,
            configuration.SystemPrompt,
            configuration.DecodingMode,
            configuration.MaxInputTokens,
            configuration.MaxOutputTokens,
            configuration.ConnectTimeoutMs,
            configuration.RequestTimeoutMs,
            configuration.MaxAttempts,
            configuration.DataRegion,
            configuration.RetentionClass,
            configuration.Status,
            configuration.IsActive,
            configuration.CreatedAt,
            configuration.UpdatedAt,
            configuration.PublishedAt,
            configuration.LastTestedAt,
            configuration.LastTestSucceeded,
            configuration.LastTestFailureCode,
            configuration.AdapterContractVersion,
            configuration.CanonicalSchemaVersion,
            configuration.CanonicalSchemaHash,
            configuration.EffectiveSchemaHash,
            configuration.SchemaTransformerVersion);
    }
}
