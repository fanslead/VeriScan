using Microsoft.EntityFrameworkCore;
using VeriScan.Application.Abstractions;
using VeriScan.Domain.Entities;

namespace VeriScan.Infrastructure.Persistence.Repositories;

public sealed partial class AdminReadStore(VeriScanDbContext dbContext) : IAdminReadStore
{
    private static readonly System.Linq.Expressions.Expression<
        Func<ModerationItem, AdminModerationRecordReadData>> RecordSelector = item =>
        new AdminModerationRecordReadData(
            item.Id,
            item.RequestId,
            item.ApplicationId,
            item.Request == null || item.Request.Application == null
                ? null
                : item.Request.Application.Name,
            item.Content.Length <= 240 ? item.Content : item.Content.Substring(0, 240),
            item.ContentHash,
            item.Decision.HasValue ? item.Decision.Value.ToString() : null,
            item.RiskScore,
            item.ScoreSource,
            item.ReviewSource,
            item.Route,
            item.ReasonCodesText,
            item.CategoriesText,
            item.EvidenceText,
            item.ErrorCode,
            item.CreatedAt,
            item.MachineCompletedAt,
            null,
            item.AiConfigurationRevision,
            item.ProviderRequestId,
            item.AiInputTokens,
            item.AiOutputTokens,
            item.AiFailureCode);

    public async Task<AdminOverviewReadData> GetOverviewAsync(
        DateTimeOffset from,
        DateTimeOffset through,
        CancellationToken cancellationToken)
    {
        var items = dbContext.ModerationItems
            .AsNoTracking()
            .Where(item => item.CreatedAt >= from && item.CreatedAt < through);

        var aggregate = await items
            .GroupBy(_ => 1)
            .Select(group => new
            {
                Total = group.LongCount(),
                Pass = group.LongCount(item => item.Decision == ModerationDecision.Pass),
                Reject = group.LongCount(item => item.Decision == ModerationDecision.Reject),
                Review = group.LongCount(item => item.Decision == ModerationDecision.Review)
            })
            .SingleOrDefaultAsync(cancellationToken);

        var requestCount = await dbContext.ModerationRequests
            .AsNoTracking()
            .LongCountAsync(
                request => request.SubmittedAt >= from && request.SubmittedAt < through,
                cancellationToken);

        var trend = await GetTrendAsync(from, through, items, cancellationToken);

        var recentRecords = await items
            .OrderByDescending(item => item.CreatedAt)
            .ThenByDescending(item => item.Id)
            .Take(8)
            .Select(RecordSelector)
            .ToListAsync(cancellationToken);

        return new AdminOverviewReadData(
            requestCount,
            aggregate?.Total ?? 0,
            aggregate?.Pass ?? 0,
            aggregate?.Reject ?? 0,
            aggregate?.Review ?? 0,
            null,
            trend,
            recentRecords,
            through);
    }

    public async Task<AdminModerationRecordPageReadData> ListRecordsAsync(
        Guid? applicationId,
        string? decision,
        string? keyword,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = BuildRecordQuery(applicationId, decision, keyword);
        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(item => item.CreatedAt)
            .ThenByDescending(item => item.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(RecordSelector)
            .ToListAsync(cancellationToken);

        return new AdminModerationRecordPageReadData(items, total);
    }

    public Task<AdminModerationRecordReadData?> GetRecordAsync(
        Guid recordId,
        CancellationToken cancellationToken)
    {
        return dbContext.ModerationItems
            .AsNoTracking()
            .Where(item => item.Id == recordId)
            .Select(RecordSelector)
            .SingleOrDefaultAsync(cancellationToken);
    }

    private IQueryable<ModerationItem> BuildRecordQuery(
        Guid? applicationId,
        string? decision,
        string? keyword)
    {
        var query = dbContext.ModerationItems.AsNoTracking();
        if (applicationId.HasValue)
        {
            query = query.Where(item => item.ApplicationId == applicationId.Value);
        }

        if (!string.IsNullOrWhiteSpace(decision) &&
            Enum.TryParse<ModerationDecision>(decision, ignoreCase: true, out var parsed))
        {
            query = query.Where(item => item.Decision == parsed);
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(item =>
                item.ClientItemId.Contains(keyword) ||
                item.Content.Contains(keyword) ||
                item.ContentHash.Contains(keyword));
        }

        return query;
    }

    private async Task<IReadOnlyList<AdminOverviewTrendReadData>> GetTrendAsync(
        DateTimeOffset from,
        DateTimeOffset through,
        IQueryable<ModerationItem> items,
        CancellationToken cancellationToken)
    {
        if (string.Equals(
                dbContext.Database.ProviderName,
                "Microsoft.EntityFrameworkCore.InMemory",
                StringComparison.Ordinal))
        {
            var trendItems = await items
                .Select(item => new { item.CreatedAt, item.Decision })
                .Take(10_000)
                .ToListAsync(cancellationToken);
            return trendItems
                .GroupBy(item => item.CreatedAt.Hour)
                .Select(group => new AdminOverviewTrendReadData(
                    group.Key,
                    group.LongCount(),
                    group.LongCount(item => item.Decision == ModerationDecision.Reject),
                    group.LongCount(item => item.Decision == ModerationDecision.Review)))
                .OrderBy(point => point.Hour)
                .ToArray();
        }

        if (dbContext.Database.IsNpgsql())
        {
            return await GetPostgresTrendAsync(from, through, cancellationToken);
        }

        return await items
            .GroupBy(item => item.CreatedAt.Hour)
            .Select(group => new AdminOverviewTrendReadData(
                group.Key,
                group.LongCount(),
                group.LongCount(item => item.Decision == ModerationDecision.Reject),
                group.LongCount(item => item.Decision == ModerationDecision.Review)))
            .OrderBy(point => point.Hour)
            .ToListAsync(cancellationToken);
    }

}
