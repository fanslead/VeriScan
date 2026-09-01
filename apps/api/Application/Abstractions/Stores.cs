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

    Task<ApplicationApiKey?> GetByPublicKeyIdAsync(string publicKeyId, CancellationToken cancellationToken);

    Task<IReadOnlyList<ApplicationApiKey>> ListByApplicationAsync(Guid applicationId, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public interface IModerationStore
{
    Task<bool> TryReserveAsync(ModerationRequest request, CancellationToken cancellationToken);

    Task<ModerationRequest?> GetByIdAsync(Guid applicationId, Guid requestId, CancellationToken cancellationToken);

    Task<ModerationRequest?> GetByIdempotencyKeyAsync(
        Guid applicationId,
        string idempotencyKeyDigest,
        CancellationToken cancellationToken);

    Task AddItemAsync(ModerationItem item, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public interface IWordRuleStore
{
    Task<IReadOnlyList<WordRule>> GetEnabledAsync(CancellationToken cancellationToken);
}
