using VeriScan.Application.Abstractions;
using VeriScan.Application.Contracts;

namespace VeriScan.Application.Services;

/// <summary>查询管理审计事实并执行统一时间及分页约束。</summary>
public interface IAuditQueryService
{
    Task<AuditEventListResponse> ListAsync(
        AuditEventQuery query,
        CancellationToken cancellationToken);
}

public sealed class AuditQueryService(IAuditReadStore store) : IAuditQueryService
{
    private static readonly TimeSpan DefaultWindow = TimeSpan.FromDays(7);
    private static readonly TimeSpan MaximumWindow = TimeSpan.FromDays(90);

    public async Task<AuditEventListResponse> ListAsync(
        AuditEventQuery query,
        CancellationToken cancellationToken)
    {
        var through = (query.Through ?? DateTimeOffset.UtcNow).ToUniversalTime();
        var from = (query.From ?? through.Subtract(DefaultWindow)).ToUniversalTime();
        var limit = query.Limit ?? 100;
        if (from >= through)
        {
            throw new RequestValidationException("统计窗口必须满足 from 早于 through。");
        }

        if (through - from > MaximumWindow)
        {
            throw new RequestValidationException("统计窗口不能超过 90 天。");
        }

        if (limit is < 1 or > 500)
        {
            throw new RequestValidationException("审计事件单页数量必须在 1 到 500 之间。");
        }

        if (query.ApplicationId is { } applicationId &&
            !await store.ApplicationExistsAsync(applicationId, cancellationToken))
        {
            throw new ResourceNotFoundException("应用不存在。");
        }

        var data = await store.ListAsync(
            new AuditReadQuery(
                query.ApplicationId,
                query.ApiKeyId,
                string.IsNullOrWhiteSpace(query.Action) ? null : query.Action.Trim(),
                from,
                through,
                limit),
            cancellationToken);
        return new AuditEventListResponse(
            data.Items.Select(item => new AuditEventResponse(
                item.Id,
                item.TenantId,
                item.ApplicationId,
                item.ApiKeyId,
                item.ActorType,
                item.ActorId,
                item.Action,
                item.ResourceType,
                item.ResourceId,
                item.BeforeJson,
                item.AfterJson,
                item.CorrelationId,
                item.OccurredAt)).ToArray(),
            data.Total,
            from,
            through);
    }
}
