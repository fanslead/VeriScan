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
        CancellationToken cancellationToken);

    Task<BatchModerationResponse> GetBatchAsync(
        Guid requestId,
        ApiKeyPrincipalData principal,
        CancellationToken cancellationToken);
}

public sealed class ModerationService(
    IModerationStore moderationStore,
    IWordRuleStore wordRuleStore,
    IRuleModerationEngine ruleModerationEngine,
    IModerationAiClient moderationAiClient,
    IContentHashService contentHashService) : IModerationService
{
    private const int MaximumContentBytes = 64 * 1024;

    public async Task<BatchModerationResponse> CreateBatchAsync(
        BatchModerationRequest request,
        ApiKeyPrincipalData principal,
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

        var submittedAt = DateTimeOffset.UtcNow;
        var moderationRequest = new ModerationRequest(
            principal.TenantId,
            principal.ApplicationId,
            principal.KeyId,
            request.Mode.ToString().ToLowerInvariant(),
            null,
            null,
            submittedAt);
        var rules = await wordRuleStore.GetEnabledAsync(cancellationToken);

        foreach (var item in request.Items)
        {
            var moderationItem = new ModerationItem(
                moderationRequest.Id,
                principal.TenantId,
                principal.ApplicationId,
                item.Id,
                item.Content,
                contentHashService.Compute(item.Content),
                item.Language,
                item.ContentType,
                submittedAt);
            var ruleEvaluation = ruleModerationEngine.Evaluate(item.Content, rules);
            var evaluation = ruleEvaluation;
            AiModerationResult? aiResult = null;
            if (ruleEvaluation.RequiresAi)
            {
                aiResult = await moderationAiClient.ModerateAsync(
                    new AiModerationRequest(
                        principal.TenantId,
                        principal.ApplicationId,
                        item.Content,
                        item.Language),
                    cancellationToken);
                evaluation = AiModerationMappings.ToEvaluation(aiResult, ruleEvaluation);
            }

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
        }

        moderationRequest.Complete(DateTimeOffset.UtcNow);
        await moderationStore.AddAsync(moderationRequest, cancellationToken);
        await moderationStore.SaveChangesAsync(cancellationToken);

        return ModerationMappings.ToResponse(moderationRequest);
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
