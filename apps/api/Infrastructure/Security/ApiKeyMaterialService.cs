using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using VeriScan.Application.Abstractions;
using VeriScan.Domain.Entities;

namespace VeriScan.Infrastructure.Security;

public sealed class ApiKeyMaterialService(
    IOptions<ApiKeyOptions> options,
    IApiKeyStore apiKeyStore,
    HybridApiKeyCache apiKeyCache)
    : IApiKeyMaterialGenerator, IApiKeyVerifier
{
    private static readonly char[] HexAlphabet = "0123456789abcdef".ToCharArray();
    private readonly ApiKeyOptions options = options.Value;

    public ApiKeyMaterial Generate(string environmentName)
    {
        var publicKeyId = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
        var secret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
        var keyPrefix = $"vsk_{environmentName}_{publicKeyId}";
        var digest = ComputeDigest(publicKeyId, secret);

        return new ApiKeyMaterial(
            publicKeyId,
            keyPrefix,
            secret[^4..],
            secret,
            digest,
            options.PepperVersion);
    }

    public async Task<ApiKeyPrincipalData?> VerifyAsync(
        string presentedKey,
        CancellationToken cancellationToken)
    {
        if (presentedKey.Length is < 50 or > 160)
        {
            return null;
        }

        var separator = presentedKey.IndexOf('.', StringComparison.Ordinal);
        if (separator <= 0 || separator == presentedKey.Length - 1)
        {
            return null;
        }

        var prefix = presentedKey[..separator];
        var secret = presentedKey[(separator + 1)..];
        var segments = prefix.Split('_', StringSplitOptions.None);
        if (segments.Length != 3 || segments[0] != "vsk" ||
            (segments[1] != "test" && segments[1] != "live") ||
            segments[2].Length != 32 || segments[2].Any(character => !IsLowerHex(character)) ||
            secret.Length != 43)
        {
            return null;
        }

        var key = await apiKeyCache.GetAsync(
            segments[2],
            async token => await apiKeyStore.GetVerificationDataAsync(segments[2], token),
            cancellationToken);
        if (key is null || !string.Equals(key.Environment, segments[1], StringComparison.Ordinal))
        {
            return null;
        }

        var expectedDigest = ComputeDigest(key.PublicKeyId, secret);
        if (!CryptographicOperations.FixedTimeEquals(expectedDigest, key.SecretDigest))
        {
            return null;
        }

        var now = DateTimeOffset.UtcNow;
        if (key.Status != ApiKeyStatus.Active || key.NotBefore > now || key.ExpiresAt <= now ||
            key.ApplicationStatus != ApplicationStatus.Active)
        {
            return null;
        }

        return new ApiKeyPrincipalData(
            key.TenantId,
            key.ApplicationId,
            key.KeyId,
            key.Environment,
            key.ScopesText.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    private byte[] ComputeDigest(string publicKeyId, string secret)
    {
        if (string.IsNullOrWhiteSpace(options.Pepper))
        {
            throw new InvalidOperationException("Security:ApiKey:Pepper 未配置。");
        }

        var input = Encoding.UTF8.GetBytes($"{publicKeyId}\0{secret}");
        return HMACSHA256.HashData(Encoding.UTF8.GetBytes(options.Pepper), input);
    }

    private static bool IsLowerHex(char value)
    {
        return Array.IndexOf(HexAlphabet, value) >= 0;
    }

}
