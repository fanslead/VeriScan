using Microsoft.EntityFrameworkCore;
using VeriScan.Application.Abstractions;

namespace VeriScan.Infrastructure.Persistence.Repositories;

/// <summary>按索引读取审计事实，不返回数据库实体。</summary>
public sealed class AuditReadStore(VeriScanDbContext dbContext) : IAuditReadStore
{
    public Task<bool> ApplicationExistsAsync(
        Guid applicationId,
        CancellationToken cancellationToken)
    {
        return dbContext.Applications
            .AsNoTracking()
            .AnyAsync(application => application.Id == applicationId, cancellationToken);
    }

    public async Task<(IReadOnlyList<AuditReadData> Items, long Total)> ListAsync(
        AuditReadQuery query,
        CancellationToken cancellationToken)
    {
        var events = dbContext.AuditEvents
            .AsNoTracking()
            .Where(auditEvent =>
                auditEvent.OccurredAt >= query.From &&
                auditEvent.OccurredAt < query.Through);
        if (query.ApplicationId is { } applicationId)
        {
            events = events.Where(auditEvent => auditEvent.ApplicationId == applicationId);
        }

        if (query.ApiKeyId is { } apiKeyId)
        {
            events = events.Where(auditEvent => auditEvent.ApiKeyId == apiKeyId);
        }

        if (!string.IsNullOrWhiteSpace(query.Action))
        {
            events = events.Where(auditEvent => auditEvent.Action == query.Action);
        }

        var total = await events.LongCountAsync(cancellationToken);
        var items = await events
            .OrderByDescending(auditEvent => auditEvent.OccurredAt)
            .ThenByDescending(auditEvent => auditEvent.Id)
            .Take(query.Limit)
            .Select(auditEvent => new AuditReadData(
                auditEvent.Id,
                auditEvent.TenantId,
                auditEvent.ApplicationId,
                auditEvent.ApiKeyId,
                auditEvent.ActorType,
                auditEvent.ActorId,
                auditEvent.Action,
                auditEvent.ResourceType,
                auditEvent.ResourceId,
                auditEvent.BeforeJson,
                auditEvent.AfterJson,
                auditEvent.CorrelationId,
                auditEvent.OccurredAt))
            .ToArrayAsync(cancellationToken);
        return (items, total);
    }
}
