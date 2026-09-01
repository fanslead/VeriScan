using VeriScan.Application.Abstractions;
using VeriScan.Application.Contracts;

namespace VeriScan.Application.Services;

/// <summary>管理端事实读取服务。</summary>
public interface IAdminReadService
{
    Task<AdminOverviewResponse> GetOverviewAsync(CancellationToken cancellationToken);

    Task<ModerationRecordPageResponse> ListRecordsAsync(
        AdminModerationRecordQuery query,
        CancellationToken cancellationToken);

    Task<ModerationRecordResponse> GetRecordAsync(
        Guid recordId,
        CancellationToken cancellationToken);
}

public sealed class AdminReadService(
    IAdminReadStore adminReadStore,
    IModerationContentProtector contentProtector) : IAdminReadService
{
    public async Task<AdminOverviewResponse> GetOverviewAsync(CancellationToken cancellationToken)
    {
        var through = DateTimeOffset.UtcNow;
        var from = new DateTimeOffset(through.UtcDateTime.Date, TimeSpan.Zero);
        var data = await adminReadStore.GetOverviewAsync(from, through, cancellationToken);
        var previous = await adminReadStore.GetOverviewAsync(
            from.AddDays(-1),
            through.AddDays(-1),
            cancellationToken);
        return AdminReadMappings.ToOverviewResponse(data, previous, contentProtector);
    }

    public async Task<ModerationRecordPageResponse> ListRecordsAsync(
        AdminModerationRecordQuery query,
        CancellationToken cancellationToken)
    {
        var normalizedQuery = AdminReadQueryPolicy.Normalize(query);

        var data = await adminReadStore.ListRecordsAsync(
            normalizedQuery.ApplicationId,
            normalizedQuery.Decision,
            normalizedQuery.Keyword,
            normalizedQuery.Page,
            normalizedQuery.PageSize,
            cancellationToken);

        return AdminReadMappings.ToPageResponse(
            data,
            normalizedQuery.Page,
            normalizedQuery.PageSize,
            contentProtector);
    }

    public async Task<ModerationRecordResponse> GetRecordAsync(
        Guid recordId,
        CancellationToken cancellationToken)
    {
        var data = await adminReadStore.GetRecordAsync(recordId, cancellationToken)
            ?? throw new ResourceNotFoundException("审核记录不存在。");
        return AdminReadMappings.ToResponse(data, contentProtector.Unprotect(data.Content));
    }
}
