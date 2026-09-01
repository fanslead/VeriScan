namespace VeriScan.Domain.Entities;

public sealed class RuleSetVersion
{
    private RuleSetVersion()
    {
    }

    public RuleSetVersion(
        string name,
        RuleNormalizationProfile normalizationProfile = RuleNormalizationProfile.Default)
    {
        Id = Guid.CreateVersion7();
        PublicRevisionId = $"ruleset@{Id:N}";
        Name = name;
        NormalizationProfile = normalizationProfile;
        Status = RuleSetStatus.Draft;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public Guid Id { get; private set; }

    public string PublicRevisionId { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public RuleNormalizationProfile NormalizationProfile { get; private set; }

    public RuleSetStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public DateTimeOffset? LastValidatedAt { get; private set; }

    public string? LastValidatedChecksum { get; private set; }

    public DateTimeOffset? PublishedAt { get; private set; }

    public string? PublishedChecksum { get; private set; }

    public ICollection<WordRule> Rules { get; private set; } = new List<WordRule>();

    public ICollection<RegexRule> RegexRules { get; private set; } = new List<RegexRule>();

    public ICollection<CombinationRule> CombinationRules { get; private set; } = new List<CombinationRule>();

    public ICollection<ApplicationEntity> Applications { get; private set; } = new List<ApplicationEntity>();

    public void ReplaceDraft(string name, IReadOnlyCollection<WordRule> rules)
    {
        ReplaceDraft(name, rules, [], [], NormalizationProfile);
    }

    public void ReplaceDraft(
        string name,
        IReadOnlyCollection<WordRule> rules,
        IReadOnlyCollection<RegexRule> regexRules,
        IReadOnlyCollection<CombinationRule> combinationRules,
        RuleNormalizationProfile normalizationProfile = RuleNormalizationProfile.Default)
    {
        EnsureDraft();
        Name = name;
        NormalizationProfile = normalizationProfile;
        Rules.Clear();
        RegexRules.Clear();
        CombinationRules.Clear();
        foreach (var rule in rules)
        {
            if (rule.RuleSetVersionId != Id)
            {
                throw new InvalidOperationException("规则不属于当前规则集版本。");
            }

            Rules.Add(rule);
        }

        foreach (var rule in regexRules)
        {
            if (rule.RuleSetVersionId != Id)
            {
                throw new InvalidOperationException("正则规则不属于当前规则集版本。");
            }

            RegexRules.Add(rule);
        }

        foreach (var rule in combinationRules)
        {
            if (rule.RuleSetVersionId != Id)
            {
                throw new InvalidOperationException("组合规则不属于当前规则集版本。");
            }

            CombinationRules.Add(rule);
        }

        LastValidatedAt = null;
        LastValidatedChecksum = null;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void RecordSuccessfulValidation(string checksum, DateTimeOffset validatedAt)
    {
        EnsureDraft();
        LastValidatedChecksum = checksum;
        LastValidatedAt = validatedAt;
        UpdatedAt = validatedAt;
    }

    public void ClearValidation()
    {
        EnsureDraft();
        LastValidatedAt = null;
        LastValidatedChecksum = null;
    }

    public void Publish(string checksum, DateTimeOffset publishedAt)
    {
        EnsureDraft();
        if (!string.Equals(LastValidatedChecksum, checksum, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("当前草稿必须通过最新内容校验后才能发布。");
        }

        Status = RuleSetStatus.Published;
        PublishedChecksum = checksum;
        PublishedAt = publishedAt;
        UpdatedAt = publishedAt;
    }

    public void Archive(DateTimeOffset archivedAt)
    {
        if (Status == RuleSetStatus.Archived)
        {
            return;
        }

        Status = RuleSetStatus.Archived;
        UpdatedAt = archivedAt;
    }

    public void RecordBindingChange(DateTimeOffset changedAt)
    {
        if (Status != RuleSetStatus.Published)
        {
            throw new InvalidOperationException("只有已发布规则集可以变更应用绑定。");
        }

        UpdatedAt = changedAt;
    }

    private void EnsureDraft()
    {
        if (Status != RuleSetStatus.Draft)
        {
            throw new InvalidOperationException("已发布或已归档的规则集不可原地修改。");
        }
    }
}

public enum RuleSetStatus
{
    Draft,
    Published,
    Archived
}
