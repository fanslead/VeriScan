using System.ComponentModel.DataAnnotations;
using VeriScan.Domain.Entities;

namespace VeriScan.Application.Contracts;

/// <summary>管理端审核记录查询参数。</summary>
public sealed record AdminModerationRecordQuery
{
    /// <summary>按应用内部标识筛选。</summary>
    public Guid? ApplicationId { get; init; }

    /// <summary>按机器决定筛选，支持 pass、reject、review。</summary>
    public string? Decision { get; init; }

    /// <summary>Decision 的兼容别名，供管理端列表筛选使用。</summary>
    public string? Status { get; init; }

    /// <summary>按客户端内容标识、原文或内容哈希检索。</summary>
    [StringLength(128)]
    public string? Keyword { get; init; }

    /// <summary>页码，从 1 开始。</summary>
    public int? Page { get; init; }

    /// <summary>每页数量，最大 100。</summary>
    public int? PageSize { get; init; }

    public const int MaximumPage = 10_000;

    public const int MaximumPageSize = 100;
}

/// <summary>管理端审核记录。</summary>
public sealed record ModerationRecordResponse(
    Guid Id,
    Guid RequestId,
    Guid ApplicationId,
    string? ApplicationName,
    string ContentPreview,
    string ContentHash,
    ModerationDecision? Decision,
    decimal? RiskScore,
    string? ScoreSource,
    string? Category,
    string? ReviewSource,
    int? DetectLevel,
    int? LatencyMs,
    DateTimeOffset CreatedAt,
    IReadOnlyList<string> ReasonCodes,
    IReadOnlyList<string> Evidence,
    string? PolicyVersion,
    string? ErrorCode,
    string? Route,
    IReadOnlyList<ModerationCategoryResponse> Categories);

/// <summary>管理端审核记录分页结果。</summary>
public sealed record ModerationRecordPageResponse(
    IReadOnlyList<ModerationRecordResponse> Items,
    int Total,
    int Page,
    int PageSize);

/// <summary>管理端按小时聚合的审核趋势。</summary>
public sealed record ModerationTrendPoint(
    string Label,
    long Total,
    long Reject,
    long Review);

/// <summary>管理端概览统计。</summary>
/// <remarks>历史对比和延迟聚合尚未落库时对应字段返回 null，不用估算值填充。</remarks>
public sealed record AdminOverviewResponse(
    long TodayRequests,
    long TodayItems,
    long PassCount,
    long RejectCount,
    long ReviewCount,
    decimal? RejectRate,
    decimal? ReviewRate,
    decimal? P95LatencyMs,
    IReadOnlyList<ModerationTrendPoint> Trend,
    IReadOnlyList<ModerationRecordResponse> RecentRecords,
    DateTimeOffset DataThrough,
    decimal? RequestDelta,
    decimal? RejectDelta,
    decimal? ReviewDelta,
    decimal? LatencyDelta);
