namespace VeriScan.Application.Abstractions;

/// <summary>管理端只读事实查询存储。</summary>
public interface IAdminReadStore
{
    Task<AdminOverviewReadData> GetOverviewAsync(
        DateTimeOffset from,
        DateTimeOffset through,
        CancellationToken cancellationToken);

    Task<AdminModerationRecordPageReadData> ListRecordsAsync(
        Guid? applicationId,
        string? decision,
        string? keyword,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    Task<AdminModerationRecordReadData?> GetRecordAsync(
        Guid recordId,
        CancellationToken cancellationToken);
}

public sealed record AdminModerationRecordReadData(
    Guid Id,
    Guid RequestId,
    Guid ApplicationId,
    string? ApplicationName,
    string Content,
    string ContentHash,
    string? Decision,
    decimal? RiskScore,
    string? ScoreSource,
    string? ReviewSource,
    string Route,
    string ReasonCodesJson,
    string CategoriesJson,
    string? ErrorCode,
    DateTimeOffset CreatedAt,
    DateTimeOffset? MachineCompletedAt,
    string? PolicyVersion);

public sealed record AdminModerationRecordPageReadData(
    IReadOnlyList<AdminModerationRecordReadData> Items,
    int Total);

public sealed record AdminOverviewReadData(
    long TodayRequests,
    long TodayItems,
    long PassCount,
    long RejectCount,
    long ReviewCount,
    decimal? P95LatencyMs,
    IReadOnlyList<AdminOverviewTrendReadData> Trend,
    IReadOnlyList<AdminModerationRecordReadData> RecentRecords,
    DateTimeOffset DataThrough);

public sealed record AdminOverviewTrendReadData(
    int Hour,
    long Total,
    long Reject,
    long Review);
