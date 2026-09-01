namespace VeriScan.Application.Contracts;

/// <summary>管理端应用用量查询参数。</summary>
public sealed record ApplicationUsageQuery
{
    /// <summary>统计窗口起点，包含该时刻；缺省为窗口结束前七天。</summary>
    public DateTimeOffset? From { get; init; }

    /// <summary>统计窗口终点，不包含该时刻；缺省为当前 UTC 时间。</summary>
    public DateTimeOffset? Through { get; init; }

    /// <summary>按创建该审核请求的 API Key 筛选，可为空表示整个应用。</summary>
    public Guid? ApiKeyId { get; init; }
}

/// <summary>应用审核用量统计结果。</summary>
/// <remarks>
/// Token 字段只汇总供应商实际返回并写入审核事实的用量；没有任何记录时返回 null，不进行估算。
/// 当前事实表按 AI 路由结果项记录调用，尚未单独保存一次调用的重试尝试明细。
/// </remarks>
public sealed record ApplicationUsageResponse(
    Guid ApplicationId,
    Guid? ApiKeyId,
    DateTimeOffset DataFrom,
    DateTimeOffset DataThrough,
    long RequestCount,
    long ItemCount,
    long PassCount,
    long RejectCount,
    long ReviewCount,
    long AiCallCount,
    long? AiInputTokens,
    long? AiOutputTokens,
    long AiFailureCount);
