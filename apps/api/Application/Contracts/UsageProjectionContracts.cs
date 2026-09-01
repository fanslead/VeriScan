namespace VeriScan.Application.Contracts;

/// <summary>管理端用量投影重建响应。</summary>
public sealed record UsageRebuildResponse(
    Guid ApplicationId,
    Guid? ApiKeyId,
    DateTimeOffset DataFrom,
    DateTimeOffset DataThrough,
    int HourlyRowsWritten,
    int DailyRowsWritten,
    long RequestCount,
    long ItemCount,
    long AiCallCount);
