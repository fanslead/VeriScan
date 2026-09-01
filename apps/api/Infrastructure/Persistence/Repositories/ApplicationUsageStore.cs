using Microsoft.EntityFrameworkCore;
using VeriScan.Application.Abstractions;
using VeriScan.Domain.Entities;

namespace VeriScan.Infrastructure.Persistence.Repositories;

/// <summary>基于审核事实表的应用用量只读查询。</summary>
public sealed class ApplicationUsageStore(VeriScanDbContext dbContext) : IApplicationUsageStore
{
    public Task<bool> ApplicationExistsAsync(
        Guid applicationId,
        CancellationToken cancellationToken)
    {
        return dbContext.Applications
            .AsNoTracking()
            .AnyAsync(application => application.Id == applicationId, cancellationToken);
    }

    public Task<bool> ApiKeyBelongsToApplicationAsync(
        Guid applicationId,
        Guid apiKeyId,
        CancellationToken cancellationToken)
    {
        return dbContext.ApplicationApiKeys
            .AsNoTracking()
            .AnyAsync(
                apiKey => apiKey.ApplicationId == applicationId && apiKey.Id == apiKeyId,
                cancellationToken);
    }

    public async Task<ApplicationUsageReadData> GetAsync(
        Guid applicationId,
        Guid? apiKeyId,
        DateTimeOffset from,
        DateTimeOffset through,
        CancellationToken cancellationToken)
    {
        var requests = dbContext.ModerationRequests
            .AsNoTracking()
            .Where(request =>
                request.ApplicationId == applicationId &&
                request.SubmittedAt >= from &&
                request.SubmittedAt < through);
        if (apiKeyId is { } keyId)
        {
            requests = requests.Where(request => request.CreatedByApiKeyId == keyId);
        }

        var items = dbContext.ModerationItems
            .AsNoTracking()
            .Where(item =>
                item.ApplicationId == applicationId &&
                item.CreatedAt >= from &&
                item.CreatedAt < through);
        if (apiKeyId is { } filteredKeyId)
        {
            items = items.Where(item => dbContext.ModerationRequests
                .AsNoTracking()
                .Any(request =>
                    request.Id == item.RequestId &&
                    request.ApplicationId == applicationId &&
                    request.CreatedByApiKeyId == filteredKeyId));
        }

        var requestCount = await requests.LongCountAsync(cancellationToken);
        var aggregate = await items
            .GroupBy(_ => 1)
            .Select(group => new
            {
                ItemCount = group.LongCount(),
                PassCount = group.LongCount(item => item.Decision == ModerationDecision.Pass),
                RejectCount = group.LongCount(item => item.Decision == ModerationDecision.Reject),
                ReviewCount = group.LongCount(item => item.Decision == ModerationDecision.Review),
                AiCallCount = group.LongCount(item =>
                    item.AiConfigurationRevision != null || item.AiFailureCode != null),
                AiInputTokenRows = group.LongCount(item => item.AiInputTokens.HasValue),
                AiInputTokens = group.Sum(item => (long?)item.AiInputTokens),
                AiOutputTokenRows = group.LongCount(item => item.AiOutputTokens.HasValue),
                AiOutputTokens = group.Sum(item => (long?)item.AiOutputTokens),
                AiFailureCount = group.LongCount(item => item.AiFailureCode != null)
            })
            .SingleOrDefaultAsync(cancellationToken);

        return new ApplicationUsageReadData(
            requestCount,
            aggregate?.ItemCount ?? 0,
            aggregate?.PassCount ?? 0,
            aggregate?.RejectCount ?? 0,
            aggregate?.ReviewCount ?? 0,
            aggregate?.AiCallCount ?? 0,
            aggregate is { AiInputTokenRows: > 0 } ? aggregate.AiInputTokens : null,
            aggregate is { AiOutputTokenRows: > 0 } ? aggregate.AiOutputTokens : null,
            aggregate?.AiFailureCount ?? 0);
    }
}
