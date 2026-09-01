using System.Text;
using System.Text.Json;
using VeriScan.Application.Abstractions;
using VeriScan.Application.Contracts;
using VeriScan.Domain.Entities;

namespace VeriScan.Application.Services;

public interface IModerationService
{
    Task<BatchModerationResponse> CreateBatchAsync(
        BatchModerationRequest request,
        ApiKeyPrincipalData principal,
        string? idempotencyKey,
        CancellationToken cancellationToken);

    Task<BatchModerationResponse> GetBatchAsync(
        Guid requestId,
        ApiKeyPrincipalData principal,
        CancellationToken cancellationToken);
}

public sealed class ModerationService(
    IModerationStore moderationStore,
    IRuleSetStore ruleSetStore,
    IRuleModerationEngine ruleModerationEngine,
    IModerationAiClient moderationAiClient,
    IContentHashService contentHashService,
    IModerationExecutionPolicy executionPolicy) : IModerationService
{
    private const int MaximumContentBytes = 64 * 1024;

    public async Task<BatchModerationResponse> CreateBatchAsync(
        BatchModerationRequest request,
        ApiKeyPrincipalData principal,
        string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (request.Mode == ModerationMode.Async)
        {
            throw new UnsupportedOperationException("异步审核将在后台任务模块启用。");
        }

        if (request.Items.Count == 0 || request.Items.Count > 100)
        {
            throw new RequestValidationException("审核内容数量必须在 1 到 100 条之间。");
        }

        var itemIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in request.Items)
        {
            if (!itemIds.Add(item.Id))
            {
                throw new RequestValidationException("同一批次中的内容标识不能重复。");
            }

            if (Encoding.UTF8.GetByteCount(item.Content) > MaximumContentBytes)
            {
                throw new RequestValidationException("单条内容超过大小限制。");
            }

            if (!string.Equals(item.ContentType, "plain_text", StringComparison.OrdinalIgnoreCase))
            {
                throw new RequestValidationException("当前版本只支持纯文本内容。");
            }
        }

        var ruleSet = await ruleSetStore.GetBoundForApplicationAsync(
            principal.ApplicationId,
            cancellationToken);
        if (ruleSet is null || ruleSet.Status != RuleSetStatus.Published)
        {
            throw new RequestConflictException("应用尚未绑定可用的已发布规则集。");
        }

        if (!string.IsNullOrWhiteSpace(request.PolicyId) &&
            !string.Equals(
                request.PolicyId.Trim(),
                ruleSet.PublicRevisionId,
                StringComparison.Ordinal))
        {
            throw new RequestConflictException("请求策略与应用当前绑定的规则集不一致。");
        }

        var identity = ModerationRequestIdentity.Create(
            principal.ApplicationId,
            idempotencyKey,
            request,
            ruleSet.PublicRevisionId,
            contentHashService);
        if (identity.IdempotencyKeyDigest is not null)
        {
            var replay = await moderationStore.GetByIdempotencyKeyAsync(
                principal.ApplicationId,
                identity.IdempotencyKeyDigest,
                cancellationToken);
            if (replay is not null)
            {
                return ResolveReplay(replay, identity.RequestFingerprint);
            }
        }

        var submittedAt = DateTimeOffset.UtcNow;
        var moderationRequest = new ModerationRequest(
            principal.TenantId,
            principal.ApplicationId,
            principal.KeyId,
            request.Mode.ToString().ToLowerInvariant(),
            ruleSet.PublicRevisionId,
            identity.IdempotencyKeyDigest,
            identity.RequestFingerprint,
            submittedAt);
        var reserved = await moderationStore.TryReserveAsync(moderationRequest, cancellationToken);
        if (!reserved)
        {
            var replay = await moderationStore.GetByIdempotencyKeyAsync(
                principal.ApplicationId,
                identity.IdempotencyKeyDigest!,
                cancellationToken)
                ?? throw new RequestConflictException("相同幂等键的请求正在建立，请稍后重试。");
            return ResolveReplay(replay, identity.RequestFingerprint);
        }

        var rules = ruleSet.Rules
            .Where(rule => rule.IsEnabled)
            .OrderByDescending(rule => rule.Weight)
            .ToArray();

        var workItems = request.Items
            .Select(item => new ModerationWorkItem(
                item,
                new ModerationItem(
                    moderationRequest.Id,
                    principal.TenantId,
                    principal.ApplicationId,
                    item.Id,
                    item.Content,
                    contentHashService.Compute(item.Content),
                    item.Language,
                    item.ContentType,
                    submittedAt),
                ruleModerationEngine.Evaluate(item.Content, rules)))
            .ToArray();

        await Parallel.ForEachAsync(
            workItems.Where(workItem => workItem.RuleEvaluation.RequiresAi),
            new ParallelOptions
            {
                MaxDegreeOfParallelism = executionPolicy.MaximumConcurrentAiCalls,
                CancellationToken = cancellationToken
            },
            async (workItem, itemCancellationToken) =>
            {
                workItem.AiResult = await moderationAiClient.ModerateAsync(
                    new AiModerationRequest(
                        principal.TenantId,
                        principal.ApplicationId,
                        workItem.Request.Content,
                        workItem.Request.Language),
                    itemCancellationToken);
            });

        foreach (var workItem in workItems)
        {
            var moderationItem = workItem.Entity;
            var aiResult = workItem.AiResult;
            var evaluation = aiResult is null
                ? workItem.RuleEvaluation
                : AiModerationMappings.ToEvaluation(aiResult, workItem.RuleEvaluation);

            moderationItem.Complete(
                evaluation.Decision,
                evaluation.ReviewSource,
                evaluation.Degraded,
                evaluation.RiskScore,
                evaluation.ScoreSource,
                evaluation.Route,
                JsonSerializer.Serialize(evaluation.ReasonCodes),
                JsonSerializer.Serialize(evaluation.Categories),
                JsonSerializer.Serialize(evaluation.Evidence),
                DateTimeOffset.UtcNow,
                aiResult?.ConfigurationRevision,
                aiResult?.ProviderRequestId,
                aiResult?.InputTokens,
                aiResult?.OutputTokens,
                aiResult?.FailureCode);
            moderationRequest.AddItem(moderationItem);
            await moderationStore.AddItemAsync(moderationItem, cancellationToken);
        }

        moderationRequest.Complete(DateTimeOffset.UtcNow);
        await moderationStore.SaveChangesAsync(cancellationToken);

        return ModerationMappings.ToResponse(moderationRequest);
    }

    private static BatchModerationResponse ResolveReplay(
        ModerationRequest existing,
        string requestFingerprint)
    {
        if (!string.Equals(
                existing.RequestFingerprint,
                requestFingerprint,
                StringComparison.Ordinal))
        {
            throw new RequestConflictException("相同幂等键已用于不同的审核请求。");
        }

        if (existing.ProcessingStatus is ModerationProcessingStatus.Completed or
            ModerationProcessingStatus.CompletedWithErrors)
        {
            return ModerationMappings.ToResponse(existing);
        }

        throw new RequestConflictException("相同幂等键的审核请求仍在处理中，请稍后重试。");
    }

    private sealed class ModerationWorkItem(
        BatchModerationItemRequest request,
        ModerationItem entity,
        RuleEvaluation ruleEvaluation)
    {
        public BatchModerationItemRequest Request { get; } = request;

        public ModerationItem Entity { get; } = entity;

        public RuleEvaluation RuleEvaluation { get; } = ruleEvaluation;

        public AiModerationResult? AiResult { get; set; }
    }

    public async Task<BatchModerationResponse> GetBatchAsync(
        Guid requestId,
        ApiKeyPrincipalData principal,
        CancellationToken cancellationToken)
    {
        var moderationRequest = await moderationStore.GetByIdAsync(
            principal.ApplicationId,
            requestId,
            cancellationToken);

        if (moderationRequest is null)
        {
            throw new ResourceNotFoundException("审核批次不存在。");
        }

        return ModerationMappings.ToResponse(moderationRequest);
    }
}
