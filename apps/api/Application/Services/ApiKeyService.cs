using VeriScan.Application.Abstractions;
using VeriScan.Application.Contracts;
using VeriScan.Domain.Entities;

namespace VeriScan.Application.Services;

public interface IApiKeyService
{
    Task<ApiKeyCreatedResponse> CreateAsync(
        Guid applicationId,
        CreateApiKeyRequest request,
        CancellationToken cancellationToken);

    Task<ApiKeyListResponse> ListAsync(Guid applicationId, CancellationToken cancellationToken);

    Task<ApiKeyCreatedResponse> RotateAsync(
        Guid applicationId,
        Guid keyId,
        RotateApiKeyRequest request,
        CancellationToken cancellationToken);

    Task RevokeAsync(Guid applicationId, Guid keyId, CancellationToken cancellationToken);
}

public sealed class ApiKeyService(
    IApplicationStore applicationStore,
    IApiKeyStore apiKeyStore,
    IApiKeyMaterialGenerator materialGenerator,
    IApiKeyPolicy apiKeyPolicy,
    IApiKeyCacheInvalidator cacheInvalidator) : IApiKeyService
{
    private static readonly StringComparer ScopeComparer = StringComparer.Ordinal;

    public Task<ApiKeyCreatedResponse> CreateAsync(
        Guid applicationId,
        CreateApiKeyRequest request,
        CancellationToken cancellationToken)
    {
        return CreateCoreAsync(
            applicationId,
            request.DisplayName,
            request.ExpiresAt,
            request.Scopes,
            null,
            cancellationToken);
    }

    public async Task<ApiKeyListResponse> ListAsync(Guid applicationId, CancellationToken cancellationToken)
    {
        _ = await applicationStore.GetByIdAsync(applicationId, cancellationToken)
            ?? throw new ResourceNotFoundException("应用不存在。");

        var keys = await apiKeyStore.ListByApplicationAsync(applicationId, cancellationToken);
        var items = keys.Select(ApiKeyMappings.ToSummary).ToArray();
        return new ApiKeyListResponse(items, items.Length);
    }

    public async Task<ApiKeyCreatedResponse> RotateAsync(
        Guid applicationId,
        Guid keyId,
        RotateApiKeyRequest request,
        CancellationToken cancellationToken)
    {
        var oldKey = await apiKeyStore.GetByIdAsync(applicationId, keyId, cancellationToken)
            ?? throw new ResourceNotFoundException("API Key 不存在。");

        return await CreateCoreAsync(
            applicationId,
            request.DisplayName,
            request.ExpiresAt,
            request.Scopes,
            request.RevokeOldKey ? oldKey : null,
            cancellationToken);
    }

    public async Task RevokeAsync(Guid applicationId, Guid keyId, CancellationToken cancellationToken)
    {
        var key = await apiKeyStore.GetByIdAsync(applicationId, keyId, cancellationToken)
            ?? throw new ResourceNotFoundException("API Key 不存在。");

        key.Revoke(DateTimeOffset.UtcNow);
        await apiKeyStore.SaveChangesAsync(cancellationToken);
        await cacheInvalidator.InvalidateAsync(key.PublicKeyId, cancellationToken);
    }

    private async Task<ApiKeyCreatedResponse> CreateCoreAsync(
        Guid applicationId,
        string displayName,
        DateTimeOffset expiresAt,
        IReadOnlyList<string> requestedScopes,
        ApplicationApiKey? keyToRevoke,
        CancellationToken cancellationToken)
    {
        var application = await applicationStore.GetByIdAsync(applicationId, cancellationToken)
            ?? throw new ResourceNotFoundException("应用不存在。");

        if (application.Status != ApplicationStatus.Active)
        {
            throw new RequestConflictException("当前应用不可创建 API Key。");
        }

        var now = DateTimeOffset.UtcNow;
        if (string.IsNullOrWhiteSpace(displayName) || displayName.Trim().Length > 100)
        {
            throw new RequestValidationException("API Key 名称长度必须在 2 到 100 个字符之间。");
        }

        displayName = displayName.Trim();
        if (displayName.Length < 2)
        {
            throw new RequestValidationException("API Key 名称长度必须在 2 到 100 个字符之间。");
        }

        if (expiresAt <= now || expiresAt > now.Add(apiKeyPolicy.MaximumLifetime))
        {
            throw new RequestValidationException("API Key 过期时间不符合安全策略。");
        }

        var scopes = requestedScopes
            .Where(scope => !string.IsNullOrWhiteSpace(scope))
            .Select(scope => scope.Trim())
            .Distinct(ScopeComparer)
            .ToArray();

        if (scopes.Length == 0 || scopes.Any(scope => !apiKeyPolicy.IsAllowedScope(scope)))
        {
            throw new RequestValidationException("API Key 授权范围无效。");
        }

        var existingKeys = await apiKeyStore.ListByApplicationAsync(applicationId, cancellationToken);
        var activeKeyCount = existingKeys.Count(key => key.IsUsable(now));
        if (activeKeyCount >= apiKeyPolicy.MaximumActiveKeys)
        {
            throw new RequestConflictException("应用的活跃 API Key 数量已达到上限。");
        }

        var material = materialGenerator.Generate(application.EnvironmentName);
        var key = new ApplicationApiKey(
            application.TenantId,
            application.Id,
            material.PublicKeyId,
            material.KeyPrefix,
            material.LastFour,
            material.SecretDigest,
            material.PepperVersion,
            string.Join(',', scopes),
            displayName,
            application.EnvironmentName,
            expiresAt);

        if (keyToRevoke is not null)
        {
            keyToRevoke.Revoke(now);
        }

        await apiKeyStore.AddAsync(key, cancellationToken);
        await apiKeyStore.SaveChangesAsync(cancellationToken);
        if (keyToRevoke is not null)
        {
            await cacheInvalidator.InvalidateAsync(keyToRevoke.PublicKeyId, cancellationToken);
        }

        return new ApiKeyCreatedResponse(
            key.Id,
            key.DisplayName,
            key.KeyPrefix,
            $"{key.KeyPrefix}.{material.Secret}",
            scopes,
            key.ExpiresAt);
    }
}
