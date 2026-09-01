using System.ComponentModel.DataAnnotations;
using VeriScan.Domain.Entities;

namespace VeriScan.Application.Contracts;

/// <summary>批量审核执行模式。</summary>
public enum ModerationMode
{
    Sync,
    Async,
    Auto
}

/// <summary>批量审核请求。</summary>
public sealed record BatchModerationRequest
{
    /// <summary>调用方选择的审核策略标识。</summary>
    [StringLength(100)]
    public string? PolicyId { get; init; }

    /// <summary>审核执行模式。</summary>
    public ModerationMode Mode { get; init; } = ModerationMode.Sync;

    /// <summary>待审核内容，数组顺序将保持在响应中。</summary>
    [Required, MinLength(1), MaxLength(1000)]
    public IReadOnlyList<BatchModerationItemRequest> Items { get; init; } = [];
}

/// <summary>批量审核中的单条内容。</summary>
public sealed record BatchModerationItemRequest
{
    /// <summary>调用方业务侧唯一标识。</summary>
    [Required, StringLength(128, MinimumLength = 1)]
    public required string Id { get; init; }

    /// <summary>待审核文本。</summary>
    [Required, StringLength(65536, MinimumLength = 1)]
    public required string Content { get; init; }

    /// <summary>调用方提供的语言提示。</summary>
    [StringLength(32)]
    public string? Language { get; init; }

    /// <summary>内容类型。</summary>
    [Required, StringLength(32)]
    public string ContentType { get; init; } = "plain_text";

    /// <summary>不会由调用方扩展的受控业务上下文。</summary>
    public BatchModerationContextRequest? Context { get; init; }
}

/// <summary>会参与规则判定、幂等指纹和审计版本的业务上下文。</summary>
public sealed record BatchModerationContextRequest
{
    [StringLength(64, MinimumLength = 1)]
    [RegularExpression("^[a-zA-Z0-9_-]+$")]
    public string? Scene { get; init; }

    [StringLength(32, MinimumLength = 1)]
    [RegularExpression("^[a-zA-Z0-9_-]+$")]
    public string? AuthorType { get; init; }
}

/// <summary>批量审核响应。</summary>
public sealed record BatchModerationResponse(
    Guid RequestId,
    Guid ApplicationId,
    string PolicyId,
    string ProcessingStatus,
    DateTimeOffset SubmittedAt,
    DateTimeOffset? MachineCompletedAt,
    DateTimeOffset? FinalizedAt,
    IReadOnlyList<ModerationItemResponse> Results);

/// <summary>单条审核结果。</summary>
public sealed record ModerationItemResponse(
    string Id,
    string ProcessingStatus,
    ModerationDecision? Decision,
    bool ReviewRequired,
    string? ReviewSource,
    bool Degraded,
    decimal? RiskScore,
    string? ScoreSource,
    IReadOnlyList<string> ReasonCodes,
    IReadOnlyList<ModerationCategoryResponse> Categories,
    string Route,
    string? ErrorCode,
    DateTimeOffset? MachineCompletedAt,
    DateTimeOffset? FinalizedAt,
    IReadOnlyList<ModerationEvidenceResponse>? Evidence = null);

/// <summary>审核结果中的风险分类。</summary>
public sealed record ModerationCategoryResponse(
    string Code,
    decimal? RiskScore);

/// <summary>审核命中的规则证据及其在原文中的位置。</summary>
public sealed record ModerationEvidenceResponse(
    string RuleId,
    string RuleKind,
    string Category,
    RuleAction Action,
    string Quote,
    int OriginalStart,
    int OriginalLength,
    int NormalizedStart,
    int NormalizedLength,
    string? EvidenceTemplate = null);
