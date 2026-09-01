namespace VeriScan.Domain.Entities;

public sealed class WordRule
{
    private WordRule()
    {
    }

    public WordRule(string term, WordRuleType type, string category, decimal weight)
    {
        Id = Guid.CreateVersion7();
        Term = term;
        Type = type;
        Category = category;
        Weight = weight;
        IsEnabled = true;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }

    public string Term { get; private set; } = string.Empty;

    public WordRuleType Type { get; private set; }

    public string Category { get; private set; } = string.Empty;

    public decimal Weight { get; private set; }

    public bool IsEnabled { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
}

public enum WordRuleType
{
    Black,
    Suspicious,
    White
}
