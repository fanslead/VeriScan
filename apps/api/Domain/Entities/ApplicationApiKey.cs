namespace VeriScan.Domain.Entities;

public sealed class ApplicationApiKey
{
    private ApplicationApiKey()
    {
    }

    public ApplicationApiKey(
        Guid tenantId,
        Guid applicationId,
        string publicKeyId,
        string keyPrefix,
        string lastFour,
        byte[] secretDigest,
        string pepperVersion,
        string scopesText,
        string displayName,
        string environmentName,
        DateTimeOffset expiresAt)
    {
        Id = Guid.CreateVersion7();
        TenantId = tenantId;
        ApplicationId = applicationId;
        PublicKeyId = publicKeyId;
        KeyPrefix = keyPrefix;
        LastFour = lastFour;
        SecretDigest = secretDigest;
        PepperVersion = pepperVersion;
        ScopesText = scopesText;
        DisplayName = displayName;
        EnvironmentName = environmentName;
        Status = ApiKeyStatus.Active;
        NotBefore = DateTimeOffset.UtcNow;
        ExpiresAt = expiresAt;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid ApplicationId { get; private set; }

    public string PublicKeyId { get; private set; } = string.Empty;

    public string KeyPrefix { get; private set; } = string.Empty;

    public string LastFour { get; private set; } = string.Empty;

    public byte[] SecretDigest { get; private set; } = [];

    public string PepperVersion { get; private set; } = string.Empty;

    public string ScopesText { get; private set; } = string.Empty;

    public string DisplayName { get; private set; } = string.Empty;

    public string EnvironmentName { get; private set; } = string.Empty;

    public ApiKeyStatus Status { get; private set; }

    public DateTimeOffset NotBefore { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public DateTimeOffset? LastUsedAt { get; private set; }

    public ApplicationEntity? Application { get; private set; }

    public void Revoke(DateTimeOffset now)
    {
        if (Status == ApiKeyStatus.Revoked)
        {
            return;
        }

        Status = ApiKeyStatus.Revoked;
        RevokedAt = now;
    }

    public bool IsUsable(DateTimeOffset now)
    {
        return Status == ApiKeyStatus.Active && NotBefore <= now && ExpiresAt > now;
    }

    public void MarkUsed(DateTimeOffset now)
    {
        LastUsedAt = now;
    }
}

public enum ApiKeyStatus
{
    Active,
    Suspended,
    Revoked
}
