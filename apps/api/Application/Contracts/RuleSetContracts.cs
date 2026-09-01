using System.ComponentModel.DataAnnotations;
using VeriScan.Domain.Entities;

namespace VeriScan.Application.Contracts;

public sealed record CreateRuleSetRequest : RuleSetDraftRequest;

public record RuleSetDraftRequest
{
    [Required, StringLength(100, MinimumLength = 2)]
    public required string Name { get; init; }

    [Required, MaxLength(5000)]
    public IReadOnlyList<WordRuleDraftRequest> Rules { get; init; } = [];

    /// <summary>规则集使用的文本规范化边界。</summary>
    public RuleNormalizationProfile NormalizationProfile { get; init; } = RuleNormalizationProfile.Default;

    /// <summary>正则表达式规则。</summary>
    [MaxLength(1000)]
    public IReadOnlyList<RegexRuleDraftRequest> RegexRules { get; init; } = [];

    /// <summary>多词组合规则。</summary>
    [MaxLength(1000)]
    public IReadOnlyList<CombinationRuleDraftRequest> CombinationRules { get; init; } = [];
}

public sealed record WordRuleDraftRequest
{
    [Required, StringLength(200, MinimumLength = 1)]
    public required string Term { get; init; }

    public required WordRuleType Type { get; init; }

    [Required, StringLength(64, MinimumLength = 1)]
    public required string Category { get; init; }

    [Range(0, 1)]
    public decimal Weight { get; init; }

    /// <summary>规则动作；未提供时由旧版词条类型自动映射。</summary>
    public RuleAction? Action { get; init; }

    /// <summary>词条匹配方式。</summary>
    public RuleMatchMode MatchMode { get; init; } = RuleMatchMode.NormalizedContains;

    /// <summary>适用语言，留空表示不限制。</summary>
    [StringLength(32)]
    public string? Language { get; init; }

    /// <summary>适用业务场景，留空表示不限制。</summary>
    [StringLength(64)]
    public string? Scene { get; init; }

    /// <summary>向调用方展示的证据模板。</summary>
    [StringLength(256)]
    public string? EvidenceTemplate { get; init; }

    /// <summary>规则优先级，数值越大越先执行。</summary>
    [Range(-100_000, 100_000)]
    public int Priority { get; init; }

    /// <summary>规则来源说明。</summary>
    [StringLength(128)]
    public string? Source { get; init; }
}

/// <summary>正则表达式规则草稿。</summary>
public sealed record RegexRuleDraftRequest
{
    /// <summary>正则表达式模式。</summary>
    [Required, StringLength(2048, MinimumLength = 1)]
    public required string Pattern { get; init; }

    /// <summary>匹配后的处理动作。</summary>
    public RuleAction Action { get; init; } = RuleAction.RiskSignal;

    /// <summary>风险分类编码。</summary>
    [Required, StringLength(64, MinimumLength = 1)]
    public required string Category { get; init; }

    /// <summary>规则权重。</summary>
    [Range(0, 1)]
    public decimal Weight { get; init; }

    /// <summary>单次匹配超时时间，单位为毫秒。</summary>
    [Range(1, 2_000)]
    public int TimeoutMs { get; init; } = 100;

    /// <summary>允许匹配的最大规范化文本长度。</summary>
    [Range(1, 65_536)]
    public int MaxInputLength { get; init; } = 65_536;

    /// <summary>正则表达式执行引擎。</summary>
    public RegexRuleEngineMode EngineMode { get; init; } = RegexRuleEngineMode.NonBacktracking;

    /// <summary>适用语言，留空表示不限制。</summary>
    [StringLength(32)]
    public string? Language { get; init; }

    /// <summary>适用业务场景，留空表示不限制。</summary>
    [StringLength(64)]
    public string? Scene { get; init; }

    /// <summary>向调用方展示的证据模板。</summary>
    [StringLength(256)]
    public string? EvidenceTemplate { get; init; }

    /// <summary>规则优先级，数值越大越先执行。</summary>
    [Range(-100_000, 100_000)]
    public int Priority { get; init; }

    /// <summary>规则来源说明。</summary>
    [StringLength(128)]
    public string? Source { get; init; }
}

/// <summary>组合规则草稿。</summary>
public sealed record CombinationRuleDraftRequest
{
    /// <summary>规则名称。</summary>
    [Required, StringLength(128, MinimumLength = 1)]
    public required string Name { get; init; }

    /// <summary>需要在窗口内同时出现的词条，至少两个。</summary>
    [Required, MinLength(2), MaxLength(16)]
    public IReadOnlyList<string> Terms { get; init; } = [];

    /// <summary>匹配后的处理动作。</summary>
    public RuleAction Action { get; init; } = RuleAction.RiskSignal;

    /// <summary>风险分类编码。</summary>
    [Required, StringLength(64, MinimumLength = 1)]
    public required string Category { get; init; }

    /// <summary>规则权重。</summary>
    [Range(0, 1)]
    public decimal Weight { get; init; }

    /// <summary>词条允许覆盖的规范化文本窗口长度。</summary>
    [Range(1, 4_096)]
    public int WindowSize { get; init; } = 64;

    /// <summary>适用语言，留空表示不限制。</summary>
    [StringLength(32)]
    public string? Language { get; init; }

    /// <summary>适用业务场景，留空表示不限制。</summary>
    [StringLength(64)]
    public string? Scene { get; init; }

    /// <summary>向调用方展示的证据模板。</summary>
    [StringLength(256)]
    public string? EvidenceTemplate { get; init; }

    /// <summary>规则优先级，数值越大越先执行。</summary>
    [Range(-100_000, 100_000)]
    public int Priority { get; init; }

    /// <summary>规则来源说明。</summary>
    [StringLength(128)]
    public string? Source { get; init; }
}

public sealed record RuleSetResponse(
    Guid Id,
    string PublicRevisionId,
    string Name,
    RuleSetStatus Status,
    int RuleCount,
    bool RulesTruncated,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? LastValidatedAt,
    string? LastValidatedChecksum,
    DateTimeOffset? PublishedAt,
    string? PublishedChecksum,
    int ApplicationCount,
    IReadOnlyList<WordRuleResponse> Rules,
    RuleNormalizationProfile NormalizationProfile = RuleNormalizationProfile.Default,
    IReadOnlyList<RegexRuleResponse>? RegexRules = null,
    IReadOnlyList<CombinationRuleResponse>? CombinationRules = null);

public sealed record WordRuleResponse(
    Guid Id,
    string Term,
    WordRuleType Type,
    string Category,
    decimal Weight,
    bool IsEnabled,
    RuleAction? Action = null,
    RuleMatchMode MatchMode = RuleMatchMode.NormalizedContains,
    string? Language = null,
    string? Scene = null,
    string? EvidenceTemplate = null,
    int Priority = 0,
    string? Source = null);

/// <summary>正则表达式规则响应。</summary>
public sealed record RegexRuleResponse(
    Guid Id,
    string Pattern,
    RuleAction Action,
    string Category,
    decimal Weight,
    int TimeoutMs,
    int MaxInputLength,
    RegexRuleEngineMode EngineMode,
    string? Language,
    string? Scene,
    string? EvidenceTemplate,
    int Priority,
    string? Source,
    bool IsEnabled);

/// <summary>组合规则响应。</summary>
public sealed record CombinationRuleResponse(
    Guid Id,
    string Name,
    IReadOnlyList<string> Terms,
    RuleAction Action,
    string Category,
    decimal Weight,
    int WindowSize,
    string? Language,
    string? Scene,
    string? EvidenceTemplate,
    int Priority,
    string? Source,
    bool IsEnabled);

public sealed record RuleSetListResponse(IReadOnlyList<RuleSetResponse> Items);

public sealed record RuleSetValidationResponse(
    bool Valid,
    string Checksum,
    int RuleCount,
    IReadOnlyList<RuleSetValidationIssue> Issues);

public sealed record RuleSetValidationIssue(
    string Code,
    string Message,
    int? RuleIndex);

public sealed record BindApplicationRuleSetRequest
{
    [Required, StringLength(80, MinimumLength = 10)]
    public required string PublicRevisionId { get; init; }
}
