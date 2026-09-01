namespace VeriScan.Application.Abstractions;

public sealed record ApiKeyMaterial(
    string PublicKeyId,
    string KeyPrefix,
    string LastFour,
    string Secret,
    byte[] SecretDigest,
    string PepperVersion);

public sealed record ApiKeyPrincipalData(
    Guid TenantId,
    Guid ApplicationId,
    Guid KeyId,
    string Environment,
    IReadOnlyList<string> Scopes);

public interface IApiKeyMaterialGenerator
{
    ApiKeyMaterial Generate(string environmentName);
}

public interface IApiKeyVerifier
{
    Task<ApiKeyPrincipalData?> VerifyAsync(string presentedKey, CancellationToken cancellationToken);
}

public interface IApiKeyPolicy
{
    int MaximumActiveKeys { get; }

    TimeSpan MaximumLifetime { get; }

    bool IsAllowedScope(string scope);
}

public interface IContentHashService
{
    string Compute(string content);
}
