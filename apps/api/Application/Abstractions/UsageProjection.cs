namespace VeriScan.Application.Abstractions;

/// <summary>按事实重建用量投影的结果。</summary>
public sealed record UsageRebuildData(
    DateTimeOffset DataFrom,
    DateTimeOffset DataThrough,
    int HourlyRowsWritten,
    int DailyRowsWritten,
    long RequestCount,
    long ItemCount,
    long AiCallCount);

/// <summary>用量投影读取和重建边界。</summary>
public interface IUsageProjectionStore
{
    Task<bool> ApplicationExistsAsync(Guid applicationId, CancellationToken cancellationToken);

    Task<bool> ApiKeyBelongsToApplicationAsync(
        Guid applicationId,
        Guid apiKeyId,
        CancellationToken cancellationToken);

    Task<UsageRebuildData> RebuildAsync(
        Guid applicationId,
        Guid? apiKeyId,
        DateTimeOffset from,
        DateTimeOffset through,
        CancellationToken cancellationToken);
}

/// <summary>管理端用量投影服务。</summary>
public interface IUsageProjectionService
{
    Task<UsageRebuildData> RebuildAsync(
        Guid applicationId,
        Guid? apiKeyId,
        DateTimeOffset? from,
        DateTimeOffset? through,
        CancellationToken cancellationToken);
}
