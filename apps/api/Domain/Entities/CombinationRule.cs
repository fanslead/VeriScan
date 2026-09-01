using System.Text.Json;

namespace VeriScan.Domain.Entities;

/// <summary>要求多个词条在限定窗口内同时出现的组合规则。</summary>
public sealed class CombinationRule
{
    private CombinationRule()
    {
    }

    public CombinationRule(
        Guid ruleSetVersionId,
        string name,
        IReadOnlyCollection<string> terms,
        RuleAction action,
        string category,
        decimal weight,
        int windowSize = 64,
        string? language = null,
        string? scene = null,
        string? evidenceTemplate = null,
        int priority = 0,
        string? source = null,
        bool isEnabled = true)
    {
        Id = Guid.CreateVersion7();
        RuleSetVersionId = ruleSetVersionId;
        Name = name;
        TermsJson = JsonSerializer.Serialize(terms);
        Action = action;
        Category = category;
        Weight = weight;
        WindowSize = windowSize;
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

    public string Name { get; private set; } = string.Empty;

    public string TermsJson { get; private set; } = "[]";

    public RuleAction Action { get; private set; }

    public string Category { get; private set; } = string.Empty;

    public decimal Weight { get; private set; }

    public int WindowSize { get; private set; }

    public string? Language { get; private set; }

    public string? Scene { get; private set; }

    public string? EvidenceTemplate { get; private set; }

    public int Priority { get; private set; }

    public string? Source { get; private set; }

    public bool IsEnabled { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public RuleSetVersion? RuleSetVersion { get; private set; }

    public IReadOnlyList<string> Terms
    {
        get
        {
            try
            {
                using var document = JsonDocument.Parse(TermsJson);
                return document.RootElement.ValueKind == JsonValueKind.Array
                    ? document.RootElement
                        .EnumerateArray()
                        .Where(item => item.ValueKind == JsonValueKind.String)
                        .Select(item => item.GetString() ?? string.Empty)
                        .ToArray()
                    : [];
            }
            catch (JsonException)
            {
                return [];
            }
        }
    }
}
