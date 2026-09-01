using VeriScan.Application.Abstractions;
using VeriScan.Application.Contracts;
using VeriScan.Domain.Entities;

namespace VeriScan.Application.Services;

public interface IRuleSetService
{
    Task<RuleSetResponse> CreateAsync(CreateRuleSetRequest request, CancellationToken cancellationToken);

    Task<RuleSetListResponse> ListAsync(CancellationToken cancellationToken);

    Task<RuleSetResponse> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<RuleSetResponse> UpdateAsync(
        Guid id,
        RuleSetDraftRequest request,
        CancellationToken cancellationToken);

    Task<RuleSetResponse> CreateRevisionAsync(Guid sourceId, CancellationToken cancellationToken);

    Task<RuleSetValidationResponse> ValidateAsync(Guid id, CancellationToken cancellationToken);

    Task<RuleSetResponse> PublishAsync(Guid id, CancellationToken cancellationToken);

    Task<RuleSetResponse> ArchiveAsync(Guid id, CancellationToken cancellationToken);
}

public sealed class RuleSetService(
    IRuleSetStore store,
    IOperationalFactService operationalFactService) : IRuleSetService
{
    public async Task<RuleSetResponse> CreateAsync(
        CreateRuleSetRequest request,
        CancellationToken cancellationToken)
    {
        var ruleSet = CreateDraft(
            request.Name,
            request.Rules,
            request.RegexRules,
            request.CombinationRules,
            request.NormalizationProfile);
        await store.AddAsync(ruleSet, cancellationToken);
        await RecordChangeAsync(ruleSet, "rule_set.created", null, cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
        return ToResponse(ruleSet);
    }

    public async Task<RuleSetListResponse> ListAsync(CancellationToken cancellationToken)
    {
        var ruleSets = await store.ListAsync(cancellationToken);
        return new RuleSetListResponse(ruleSets.Select(ruleSet => ToResponse(ruleSet, false)).ToArray());
    }

    public async Task<RuleSetResponse> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        return ToResponse(await GetRequiredAsync(id, cancellationToken));
    }

    public async Task<RuleSetResponse> UpdateAsync(
        Guid id,
        RuleSetDraftRequest request,
        CancellationToken cancellationToken)
    {
        var ruleSet = await GetRequiredAsync(id, cancellationToken);
        EnsureDraft(ruleSet);
        EnsureAtLeastOneRule(request.Rules.Count, request.RegexRules.Count, request.CombinationRules.Count);
        var beforeJson = OperationalFactPayloads.RuleSet(ruleSet, "before_update");
        await RecordChangeAsync(ruleSet, "rule_set.updated", beforeJson, cancellationToken);
        await ExecuteMutationAsync(
            () => store.ReplaceDraftAsync(
                ruleSet,
                NormalizeName(request.Name),
                CreateRules(ruleSet.Id, request.Rules),
                CreateRegexRules(ruleSet.Id, request.RegexRules),
                CreateCombinationRules(ruleSet.Id, request.CombinationRules),
                request.NormalizationProfile,
                cancellationToken));
        return ToResponse(ruleSet);
    }

    public async Task<RuleSetResponse> CreateRevisionAsync(
        Guid sourceId,
        CancellationToken cancellationToken)
    {
        var source = await GetRequiredAsync(sourceId, cancellationToken);
        var revision = new RuleSetVersion(source.Name, source.NormalizationProfile);
        revision.ReplaceDraft(
            source.Name,
            source.Rules
                .OrderBy(rule => rule.CreatedAt)
                .Select(rule => new WordRule(
                    revision.Id,
                    rule.Term,
                    rule.Type,
                    rule.Category,
                    rule.Weight,
                    rule.Action,
                    rule.MatchMode,
                    rule.Language,
                    rule.Scene,
                    rule.EvidenceTemplate,
                    rule.Priority,
                    rule.Source,
                    rule.IsEnabled))
                .ToArray(),
            source.RegexRules
                .OrderBy(rule => rule.CreatedAt)
                .Select(rule => new RegexRule(
                    revision.Id,
                    rule.Pattern,
                    rule.Action,
                    rule.Category,
                    rule.Weight,
                    rule.TimeoutMs,
                    rule.MaxInputLength,
                    rule.EngineMode,
                    rule.Language,
                    rule.Scene,
                    rule.EvidenceTemplate,
                    rule.Priority,
                    rule.Source,
                    rule.IsEnabled))
                .ToArray(),
            source.CombinationRules
                .OrderBy(rule => rule.CreatedAt)
                .Select(rule => new CombinationRule(
                    revision.Id,
                    rule.Name,
                    rule.Terms,
                    rule.Action,
                    rule.Category,
                    rule.Weight,
                    rule.WindowSize,
                    rule.Language,
                    rule.Scene,
                    rule.EvidenceTemplate,
                    rule.Priority,
                    rule.Source,
                    rule.IsEnabled))
                .ToArray(),
            source.NormalizationProfile);
        await store.AddAsync(revision, cancellationToken);
        await RecordChangeAsync(revision, "rule_set.revision_created", null, cancellationToken);
        await ExecuteMutationAsync(() => store.SaveChangesAsync(cancellationToken));
        return ToResponse(revision);
    }

    public async Task<RuleSetValidationResponse> ValidateAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var ruleSet = await GetRequiredAsync(id, cancellationToken);
        EnsureDraft(ruleSet);
        var validation = RuleSetPolicyValidator.Validate(ruleSet);
        if (validation.Valid)
        {
            ruleSet.RecordSuccessfulValidation(validation.Checksum, DateTimeOffset.UtcNow);
        }
        else
        {
            ruleSet.ClearValidation();
        }

        await RecordChangeAsync(ruleSet, "rule_set.validated", null, cancellationToken);
        await ExecuteMutationAsync(() => store.SaveChangesAsync(cancellationToken));
        return validation;
    }

    public async Task<RuleSetResponse> PublishAsync(Guid id, CancellationToken cancellationToken)
    {
        var ruleSet = await GetRequiredAsync(id, cancellationToken);
        EnsureDraft(ruleSet);
        var validation = RuleSetPolicyValidator.Validate(ruleSet);
        if (!validation.Valid)
        {
            throw new RequestConflictException("规则集仍存在校验问题，不能发布。");
        }

        if (ruleSet.LastValidatedAt is null ||
            !string.Equals(
                ruleSet.LastValidatedChecksum,
                validation.Checksum,
                StringComparison.Ordinal))
        {
            throw new RequestConflictException("发布前必须对当前草稿执行一次成功校验。");
        }

        var beforeJson = OperationalFactPayloads.RuleSet(ruleSet, "before_publish");
        ruleSet.Publish(validation.Checksum, DateTimeOffset.UtcNow);
        await RecordChangeAsync(ruleSet, "rule_set.published", beforeJson, cancellationToken);
        await ExecuteMutationAsync(() => store.SaveChangesAsync(cancellationToken));
        return ToResponse(ruleSet);
    }

    public async Task<RuleSetResponse> ArchiveAsync(Guid id, CancellationToken cancellationToken)
    {
        var ruleSet = await GetRequiredAsync(id, cancellationToken);
        var bindingCount = await store.CountBindingsAsync(ruleSet.Id, cancellationToken);
        if (bindingCount > 0)
        {
            throw new RequestConflictException("仍有应用绑定该规则集，请先切换应用规则版本。");
        }

        var beforeJson = OperationalFactPayloads.RuleSet(ruleSet, "before_archive");
        ruleSet.Archive(DateTimeOffset.UtcNow);
        await RecordChangeAsync(ruleSet, "rule_set.archived", beforeJson, cancellationToken);
        await ExecuteMutationAsync(() => store.SaveChangesAsync(cancellationToken));
        return ToResponse(ruleSet);
    }

    private static RuleSetVersion CreateDraft(
        string name,
        IReadOnlyList<WordRuleDraftRequest> ruleRequests,
        IReadOnlyList<RegexRuleDraftRequest> regexRuleRequests,
        IReadOnlyList<CombinationRuleDraftRequest> combinationRuleRequests,
        RuleNormalizationProfile normalizationProfile)
    {
        EnsureAtLeastOneRule(ruleRequests.Count, regexRuleRequests.Count, combinationRuleRequests.Count);
        var ruleSet = new RuleSetVersion(NormalizeName(name), normalizationProfile);
        ruleSet.ReplaceDraft(
            ruleSet.Name,
            CreateRules(ruleSet.Id, ruleRequests),
            CreateRegexRules(ruleSet.Id, regexRuleRequests),
            CreateCombinationRules(ruleSet.Id, combinationRuleRequests),
            normalizationProfile);
        return ruleSet;
    }

    private static WordRule[] CreateRules(
        Guid ruleSetVersionId,
        IReadOnlyList<WordRuleDraftRequest> requests)
    {
        return requests.Select(request => new WordRule(
            ruleSetVersionId,
            request.Term.Trim(),
            request.Type,
            request.Category.Trim().ToLowerInvariant(),
            request.Weight,
            request.Action,
            request.MatchMode,
            request.Language?.Trim(),
            request.Scene?.Trim(),
            request.EvidenceTemplate?.Trim(),
            request.Priority,
            request.Source?.Trim())).ToArray();
    }

    private static void EnsureAtLeastOneRule(
        int wordRuleCount,
        int regexRuleCount,
        int combinationRuleCount)
    {
        if (wordRuleCount + regexRuleCount + combinationRuleCount == 0)
        {
            throw new RequestValidationException("规则集至少需要一条规则。");
        }
    }

    private static RegexRule[] CreateRegexRules(
        Guid ruleSetVersionId,
        IReadOnlyList<RegexRuleDraftRequest> requests)
    {
        return requests.Select(request => new RegexRule(
            ruleSetVersionId,
            request.Pattern,
            request.Action,
            request.Category.Trim().ToLowerInvariant(),
            request.Weight,
            request.TimeoutMs,
            request.MaxInputLength,
            request.EngineMode,
            request.Language?.Trim(),
            request.Scene?.Trim(),
            request.EvidenceTemplate?.Trim(),
            request.Priority,
            request.Source?.Trim())).ToArray();
    }

    private static CombinationRule[] CreateCombinationRules(
        Guid ruleSetVersionId,
        IReadOnlyList<CombinationRuleDraftRequest> requests)
    {
        return requests.Select(request => new CombinationRule(
            ruleSetVersionId,
            request.Name.Trim(),
            request.Terms.Select(term => term.Trim()).ToArray(),
            request.Action,
            request.Category.Trim().ToLowerInvariant(),
            request.Weight,
            request.WindowSize,
            request.Language?.Trim(),
            request.Scene?.Trim(),
            request.EvidenceTemplate?.Trim(),
            request.Priority,
            request.Source?.Trim())).ToArray();
    }

    private static string NormalizeName(string name)
    {
        var normalized = name.Trim();
        if (normalized.Length is < 2 or > 100)
        {
            throw new RequestValidationException("规则集名称长度必须在 2 到 100 个字符之间。");
        }

        return normalized;
    }

    private static void EnsureDraft(RuleSetVersion ruleSet)
    {
        if (ruleSet.Status != RuleSetStatus.Draft)
        {
            throw new RequestConflictException("已发布或已归档的规则集不可原地修改，请创建新草稿。");
        }
    }

    private async Task<RuleSetVersion> GetRequiredAsync(Guid id, CancellationToken cancellationToken)
    {
        return await store.GetByIdAsync(id, cancellationToken)
            ?? throw new ResourceNotFoundException("规则集不存在。");
    }

    private static async Task ExecuteMutationAsync(Func<Task> mutation)
    {
        try
        {
            await mutation();
        }
        catch (DataConcurrencyException)
        {
            throw new RequestConflictException("规则集已被其他请求修改，请刷新后重试。");
        }
    }

    private static RuleSetResponse ToResponse(RuleSetVersion ruleSet, bool includeAllRules = true)
    {
        const int previewRuleCount = 8;
        var orderedRules = ruleSet.Rules
            .OrderBy(rule => rule.Type)
            .ThenBy(rule => rule.Category)
            .ThenBy(rule => rule.Term);
        var responseRules = includeAllRules ? orderedRules : orderedRules.Take(previewRuleCount);
        var totalRuleCount = ruleSet.Rules.Count +
            ruleSet.RegexRules.Count +
            ruleSet.CombinationRules.Count;
        return new RuleSetResponse(
            ruleSet.Id,
            ruleSet.PublicRevisionId,
            ruleSet.Name,
            ruleSet.Status,
            totalRuleCount,
            !includeAllRules && totalRuleCount > previewRuleCount,
            ruleSet.CreatedAt,
            ruleSet.UpdatedAt,
            ruleSet.LastValidatedAt,
            ruleSet.LastValidatedChecksum,
            ruleSet.PublishedAt,
            ruleSet.PublishedChecksum,
            ruleSet.Applications.Count,
            responseRules
                .Select(rule => new WordRuleResponse(
                    rule.Id,
                    rule.Term,
                    rule.Type,
                    rule.Category,
                    rule.Weight,
                    rule.IsEnabled,
                    rule.Action,
                    rule.MatchMode,
                    rule.Language,
                    rule.Scene,
                    rule.EvidenceTemplate,
                    rule.Priority,
                    rule.Source))
                .ToArray(),
            ruleSet.NormalizationProfile,
            includeAllRules
                ? ruleSet.RegexRules
                    .OrderBy(rule => rule.Priority)
                    .ThenBy(rule => rule.Pattern)
                    .Select(rule => new RegexRuleResponse(
                        rule.Id,
                        rule.Pattern,
                        rule.Action,
                        rule.Category,
                        rule.Weight,
                        rule.TimeoutMs,
                        rule.MaxInputLength,
                        rule.EngineMode,
                        rule.Language,
                        rule.Scene,
                        rule.EvidenceTemplate,
                        rule.Priority,
                        rule.Source,
                        rule.IsEnabled))
                    .ToArray()
                : [],
            includeAllRules
                ? ruleSet.CombinationRules
                    .OrderBy(rule => rule.Priority)
                    .ThenBy(rule => rule.Name)
                    .Select(rule => new CombinationRuleResponse(
                        rule.Id,
                        rule.Name,
                        rule.Terms,
                        rule.Action,
                        rule.Category,
                        rule.Weight,
                        rule.WindowSize,
                        rule.Language,
                        rule.Scene,
                        rule.EvidenceTemplate,
                        rule.Priority,
                        rule.Source,
                        rule.IsEnabled))
                    .ToArray()
                : []);
    }

    private async Task RecordChangeAsync(
        RuleSetVersion ruleSet,
        string action,
        string? beforeJson,
        CancellationToken cancellationToken)
    {
        var afterJson = OperationalFactPayloads.RuleSet(ruleSet, action);
        await operationalFactService.RecordAuditAsync(
            new AuditEntry(
                null,
                null,
                null,
                "admin",
                null,
                action,
                "rule_set",
                ruleSet.Id.ToString(),
                beforeJson,
                afterJson,
                null,
                ruleSet.UpdatedAt),
            cancellationToken);
        await operationalFactService.EnqueueAsync(
            new OutboxMessage(
                action,
                "rule_set",
                ruleSet.Id,
                null,
                null,
                afterJson,
                ruleSet.UpdatedAt),
            cancellationToken);
    }
}
