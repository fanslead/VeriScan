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

public sealed class RuleSetService(IRuleSetStore store) : IRuleSetService
{
    public async Task<RuleSetResponse> CreateAsync(
        CreateRuleSetRequest request,
        CancellationToken cancellationToken)
    {
        var ruleSet = CreateDraft(request.Name, request.Rules);
        await store.AddAsync(ruleSet, cancellationToken);
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
        await ExecuteMutationAsync(
            () => store.ReplaceDraftAsync(
                ruleSet,
                NormalizeName(request.Name),
                CreateRules(ruleSet.Id, request.Rules),
                cancellationToken));
        return ToResponse(ruleSet);
    }

    public async Task<RuleSetResponse> CreateRevisionAsync(
        Guid sourceId,
        CancellationToken cancellationToken)
    {
        var source = await GetRequiredAsync(sourceId, cancellationToken);
        var revision = new RuleSetVersion(source.Name);
        revision.ReplaceDraft(
            source.Name,
            source.Rules
                .OrderBy(rule => rule.CreatedAt)
                .Select(rule => new WordRule(
                    revision.Id,
                    rule.Term,
                    rule.Type,
                    rule.Category,
                    rule.Weight))
                .ToArray());
        await store.AddAsync(revision, cancellationToken);
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

        ruleSet.Publish(validation.Checksum, DateTimeOffset.UtcNow);
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

        ruleSet.Archive(DateTimeOffset.UtcNow);
        await ExecuteMutationAsync(() => store.SaveChangesAsync(cancellationToken));
        return ToResponse(ruleSet);
    }

    private static RuleSetVersion CreateDraft(
        string name,
        IReadOnlyList<WordRuleDraftRequest> ruleRequests)
    {
        var ruleSet = new RuleSetVersion(NormalizeName(name));
        ruleSet.ReplaceDraft(
            ruleSet.Name,
            CreateRules(ruleSet.Id, ruleRequests));
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
            request.Weight)).ToArray();
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
        return new RuleSetResponse(
            ruleSet.Id,
            ruleSet.PublicRevisionId,
            ruleSet.Name,
            ruleSet.Status,
            ruleSet.Rules.Count,
            !includeAllRules && ruleSet.Rules.Count > previewRuleCount,
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
                    rule.IsEnabled))
                .ToArray());
    }
}
