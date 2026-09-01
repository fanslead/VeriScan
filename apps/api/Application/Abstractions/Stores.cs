using VeriScan.Domain.Entities;

namespace VeriScan.Application.Abstractions;

public interface IApplicationStore
{
    Task AddAsync(ApplicationEntity application, CancellationToken cancellationToken);

    Task<ApplicationEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<ApplicationEntity>> ListAsync(CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public interface IApiKeyStore
{
    Task AddAsync(ApplicationApiKey apiKey, CancellationToken cancellationToken);

    Task<ApplicationApiKey?> GetByIdAsync(Guid applicationId, Guid keyId, CancellationToken cancellationToken);

    Task<ApiKeyVerificationData?> GetVerificationDataAsync(
        string publicKeyId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ApplicationApiKey>> ListByApplicationAsync(Guid applicationId, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public sealed record ApiKeyVerificationData(
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
    ApplicationStatus ApplicationStatus);

public interface IModerationStore
{
    Task<bool> TryReserveAsync(
        ModerationRequest request,
        ModerationJob? job,
        CancellationToken cancellationToken);

    Task<ModerationRequest?> GetByIdAsync(Guid applicationId, Guid requestId, CancellationToken cancellationToken);

    Task<ModerationRequest?> GetByIdempotencyKeyAsync(
        Guid applicationId,
        string idempotencyKeyDigest,
        CancellationToken cancellationToken);

    Task<ModerationRequest?> GetForProcessingAsync(
        Guid requestId,
        CancellationToken cancellationToken);

    Task AddItemAsync(ModerationItem item, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public interface IModerationJobStore
{
    Task<ModerationJob?> ClaimNextAsync(
        string workerId,
        DateTimeOffset now,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken);

    Task<ModerationJob?> GetByRequestIdAsync(
        Guid applicationId,
        Guid requestId,
        CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public interface IRuleSetStore
{
    Task AddAsync(RuleSetVersion ruleSet, CancellationToken cancellationToken);

    Task<RuleSetVersion?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<RuleSetVersion?> GetByPublicRevisionIdAsync(
        string publicRevisionId,
        CancellationToken cancellationToken);

    Task<RuleSetVersion?> GetLatestPublishedAsync(CancellationToken cancellationToken);

    Task<RuleSetVersion?> GetBoundForApplicationAsync(
        Guid applicationId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<RuleSetVersion>> ListAsync(CancellationToken cancellationToken);

    Task<int> CountBindingsAsync(Guid ruleSetVersionId, CancellationToken cancellationToken);

    Task ReplaceDraftAsync(
        RuleSetVersion ruleSet,
        string name,
        IReadOnlyCollection<WordRule> rules,
        IReadOnlyCollection<RegexRule> regexRules,
        IReadOnlyCollection<CombinationRule> combinationRules,
        RuleNormalizationProfile normalizationProfile,
        CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
