namespace VeriScan.Domain.Entities;

public sealed class WordRule
{
    private WordRule()
    {
    }

    public WordRule(
        Guid ruleSetVersionId,
        string term,
        WordRuleType type,
        string category,
        decimal weight,
        RuleAction? action = null,
        RuleMatchMode matchMode = RuleMatchMode.NormalizedContains,
        string? language = null,
        string? scene = null,
        string? evidenceTemplate = null,
        int priority = 0,
        string? source = null,
        bool isEnabled = true)
    {
        Id = Guid.CreateVersion7();
        RuleSetVersionId = ruleSetVersionId;
        Term = term;
        Type = type;
        Category = category;
        Weight = weight;
        Action = action ?? type switch
        {
            WordRuleType.Black => RuleAction.HardReject,
            WordRuleType.White => RuleAction.ContextException,
            _ => RuleAction.RiskSignal
        };
        MatchMode = matchMode;
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

    public string Term { get; private set; } = string.Empty;

    public WordRuleType Type { get; private set; }

    public string Category { get; private set; } = string.Empty;

    public decimal Weight { get; private set; }

    public RuleAction Action { get; private set; }

    public RuleMatchMode MatchMode { get; private set; }

    public string? Language { get; private set; }

    public string? Scene { get; private set; }

    public string? EvidenceTemplate { get; private set; }

    public int Priority { get; private set; }

    public string? Source { get; private set; }

    public bool IsEnabled { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public RuleSetVersion? RuleSetVersion { get; private set; }
}

public enum WordRuleType
{
    Black,
    Suspicious,
    White
}
