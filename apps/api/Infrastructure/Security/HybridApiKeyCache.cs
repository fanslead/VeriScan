using Microsoft.Extensions.Caching.Hybrid;
using VeriScan.Application.Abstractions;
using VeriScan.Domain.Entities;

namespace VeriScan.Infrastructure.Security;

public sealed class HybridApiKeyCache(HybridCache cache) : IApiKeyCacheInvalidator
{
    private static readonly HybridCacheEntryOptions EntryOptions = new()
    {
        Expiration = TimeSpan.FromMinutes(2),
        LocalCacheExpiration = TimeSpan.FromSeconds(15)
    };

    public async ValueTask<ApiKeyVerificationData?> GetAsync(
        string publicKeyId,
        Func<CancellationToken, ValueTask<ApiKeyVerificationData?>> factory,
        CancellationToken cancellationToken)
    {
        var entry = await cache.GetOrCreateAsync(
            GetCacheKey(publicKeyId),
            async token => CachedApiKeyEntry.From(await factory(token)),
            EntryOptions,
            [GetTag(publicKeyId)],
            cancellationToken);
        return entry.ToVerificationData();
    }

    public async ValueTask InvalidateAsync(
        string publicKeyId,
        CancellationToken cancellationToken)
    {
        await cache.RemoveByTagAsync(GetTag(publicKeyId), cancellationToken);
        await cache.RemoveAsync(GetCacheKey(publicKeyId), cancellationToken);
    }

    public async ValueTask InvalidateManyAsync(
        IReadOnlyCollection<string> publicKeyIds,
        CancellationToken cancellationToken)
    {
        foreach (var publicKeyId in publicKeyIds.Distinct(StringComparer.Ordinal))
        {
            await InvalidateAsync(publicKeyId, cancellationToken);
        }
    }

    private static string GetCacheKey(string publicKeyId) => $"veriscan:auth:key:{publicKeyId}";

    private static string GetTag(string publicKeyId) => $"veriscan:auth:key-tag:{publicKeyId}";

    private sealed record CachedApiKeyEntry(
        bool Exists,
        Guid KeyId,
        Guid TenantId,
        Guid ApplicationId,
        string PublicKeyId,
        byte[] SecretDigest,
        string PepperVersion,
        string ScopesText,
        string Environment,
        ApiKeyStatus Status,
        DateTimeOffset NotBefore,
        DateTimeOffset ExpiresAt,
        ApplicationStatus ApplicationStatus)
    {
        public static CachedApiKeyEntry From(ApiKeyVerificationData? data)
        {
            return data is null
                ? new CachedApiKeyEntry(
                    false,
                    Guid.Empty,
                    Guid.Empty,
                    Guid.Empty,
                    string.Empty,
                    [],
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    default,
                    default,
                    default,
                    default)
                : new CachedApiKeyEntry(
                    true,
                    data.KeyId,
                    data.TenantId,
                    data.ApplicationId,
                    data.PublicKeyId,
                    data.SecretDigest,
                    data.PepperVersion,
                    data.ScopesText,
                    data.Environment,
                    data.Status,
                    data.NotBefore,
                    data.ExpiresAt,
                    data.ApplicationStatus);
        }

        public ApiKeyVerificationData? ToVerificationData()
        {
            return Exists
                ? new ApiKeyVerificationData(
                    KeyId,
                    TenantId,
                    ApplicationId,
                    PublicKeyId,
                    SecretDigest,
                    PepperVersion,
                    ScopesText,
                    Environment,
                    Status,
                    NotBefore,
                    ExpiresAt,
                    ApplicationStatus)
                : null;
        }
    }
}
