namespace VeriScan.Domain.Entities;

/// <summary>规则集中的安全正则规则。</summary>
public sealed class RegexRule
{
    private RegexRule()
    {
    }

    public RegexRule(
        Guid ruleSetVersionId,
        string pattern,
        RuleAction action,
        string category,
        decimal weight,
        int timeoutMs = 100,
        int maxInputLength = 65_536,
        RegexRuleEngineMode engineMode = RegexRuleEngineMode.NonBacktracking,
        string? language = null,
        string? scene = null,
        string? evidenceTemplate = null,
        int priority = 0,
        string? source = null,
        bool isEnabled = true)
    {
        Id = Guid.CreateVersion7();
        RuleSetVersionId = ruleSetVersionId;
        Pattern = pattern;
        Action = action;
        Category = category;
        Weight = weight;
        TimeoutMs = timeoutMs;
        MaxInputLength = maxInputLength;
        EngineMode = engineMode;
        Language = language;
        Scene = scene;
        EvidenceTemplate = evidenceTemplate;
        Priority = priority;
        Source = source;
        IsEnabled = isEnabled;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }

    public Guid RuleSetVersionId { get; private set; }

    public string Pattern { get; private set; } = string.Empty;

    public RuleAction Action { get; private set; }

    public string Category { get; private set; } = string.Empty;

    public decimal Weight { get; private set; }

    public int TimeoutMs { get; private set; }

    public int MaxInputLength { get; private set; }

    public RegexRuleEngineMode EngineMode { get; private set; }

    public string? Language { get; private set; }

    public string? Scene { get; private set; }

    public string? EvidenceTemplate { get; private set; }

    public int Priority { get; private set; }

    public string? Source { get; private set; }

    public bool IsEnabled { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public RuleSetVersion? RuleSetVersion { get; private set; }
}
