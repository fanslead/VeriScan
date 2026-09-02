using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
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

    Task<BatchModerationResponse> CancelBatchAsync(
        Guid requestId,
        ApiKeyPrincipalData principal,
        string? idempotencyKey,
        CancellationToken cancellationToken);

    Task ProcessQueuedBatchAsync(Guid requestId, CancellationToken cancellationToken);

    Task FinalizeDeadLetterAsync(Guid requestId, CancellationToken cancellationToken);
}

public sealed class ModerationService(
    IModerationStore moderationStore,
    IModerationJobStore moderationJobStore,
    IModerationCancellationStore moderationCancellationStore,
    IRuleSetStore ruleSetStore,
    IRuleModerationEngine ruleModerationEngine,
    IModerationAiClient moderationAiClient,
    IContentHashService contentHashService,
    IIdempotencyDigestService idempotencyDigestService,
    IModerationContentProtector contentProtector,
    IModerationExecutionPolicy executionPolicy,
    IModerationQueuePolicy queuePolicy,
    IModerationIdempotencyPolicy idempotencyPolicy,
    IWebhookPublicationService webhookPublicationService,
    IOperationalFactService operationalFactService) : IModerationService
{
    private const int MaximumContentBytes = 64 * 1024;
    private const int HttpOkStatus = 200;
    private const int HttpAcceptedStatus = 202;
    private const int HttpNotFoundStatus = 404;
    private const int HttpConflictStatus = 409;
    private static readonly JsonSerializerOptions IdempotentResponseJsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public async Task<BatchModerationResponse> CreateBatchAsync(
        BatchModerationRequest request,
        ApiKeyPrincipalData principal,
        string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        var requestStopwatch = Stopwatch.StartNew();
        ValidateRequest(request);

        var ruleSet = await GetPublishedRuleSetAsync(
            principal.ApplicationId,
            request.PolicyId,
            cancellationToken);
        var identity = ModerationRequestIdentity.Create(
            principal.ApplicationId,
            idempotencyKey,
            request,
            ruleSet.PublicRevisionId,
            idempotencyDigestService);
        if (identity.IdempotencyKeyDigest is not null)
        {
            var replay = await moderationStore.GetByIdempotencyKeyAsync(
                principal.ApplicationId,
                identity.IdempotencyKeyDigest,
                cancellationToken);
            if (replay is not null)
            {
                return await ResolveReplayAsync(
                    replay,
                    identity.RequestFingerprint,
                    principal,
                    request.Items.Count,
                    "replay",
                    requestStopwatch,
                    cancellationToken);
            }
        }

        var rules = ruleSet.Rules
            .Where(rule => rule.IsEnabled)
            .OrderByDescending(rule => rule.Weight)
            .ToArray();
        var normalizationOptions = RuleNormalizationOptions.ForProfile(ruleSet.NormalizationProfile);
        var compiledPolicy = ruleModerationEngine.GetOrCompile(
            ruleSet.PublicRevisionId,
            rules,
            ruleSet.RegexRules.Where(rule => rule.IsEnabled).ToArray(),
            ruleSet.CombinationRules.Where(rule => rule.IsEnabled).ToArray(),
            normalizationOptions);
        var evaluations = request.Items
            .Select(item => compiledPolicy.Evaluate(
                item.Content,
                item.Language,
                item.Context?.Scene))
            .ToArray();
        var enqueue = ShouldEnqueue(request, evaluations);
        var submittedAt = DateTimeOffset.UtcNow;
        var initialStatus = enqueue
            ? ModerationProcessingStatus.Accepted
            : ModerationProcessingStatus.Processing;
        var moderationRequest = new ModerationRequest(
            principal.TenantId,
            principal.ApplicationId,
            principal.KeyId,
            request.Mode.ToString().ToLowerInvariant(),
            ruleSet.PublicRevisionId,
            identity.IdempotencyKeyDigest,
            identity.RequestFingerprint,
            submittedAt,
            initialStatus);

        var workItems = new ModerationWorkItem[request.Items.Count];
        for (var index = 0; index < request.Items.Count; index++)
        {
            var item = request.Items[index];
            var entity = new ModerationItem(
                moderationRequest.Id,
                principal.TenantId,
                principal.ApplicationId,
                index,
                item.Id,
                contentProtector.Protect(item.Content),
                contentHashService.Compute(item.Content),
                contentHashService.KeyVersion,
                item.Language,
                item.ContentType,
                item.Context?.Scene,
                item.Context?.AuthorType,
                submittedAt,
                initialStatus);
            moderationRequest.AddItem(entity);
            workItems[index] = new ModerationWorkItem(
                entity,
                item.Content,
                evaluations[index],
                normalizationOptions);
        }

        var job = enqueue
            ? new ModerationJob(
                principal.TenantId,
                principal.ApplicationId,
                moderationRequest.Id,
                priority: 0,
                maximumAttempts: queuePolicy.MaximumAttempts,
                submittedAt)
            : null;
        var reserved = await moderationStore.TryReserveAsync(
            moderationRequest,
            job,
            cancellationToken);
        if (!reserved)
        {
            var replay = await moderationStore.GetByIdempotencyKeyAsync(
                principal.ApplicationId,
                identity.IdempotencyKeyDigest!,
                cancellationToken)
                ?? throw new RequestConflictException("相同幂等键的请求正在建立，请稍后重试。");
            return await ResolveReplayAsync(
                replay,
                identity.RequestFingerprint,
                principal,
                request.Items.Count,
                "reservation_race_replay",
                requestStopwatch,
                cancellationToken);
        }

        if (enqueue)
        {
            await RecordSubmissionFactAsync(
                moderationRequest,
                principal,
                request.Items.Count,
                identity.IdempotencyKeyDigest is null ? "new" : "new_idempotent",
                HttpAcceptedStatus,
                requestStopwatch,
                "accepted",
                cancellationToken);
            return ModerationMappings.ToResponse(moderationRequest);
        }

        using var deadlineSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadlineSource.CancelAfter(executionPolicy.SynchronousDeadline);
        try
        {
            await ExecuteAsync(
                moderationRequest,
                workItems,
                1,
                identity.IdempotencyKeyDigest is null ? "new" : "new_idempotent",
                requestStopwatch,
                true,
                false,
                deadlineSource.Token);
        }
        catch (OperationCanceledException) when (
            !cancellationToken.IsCancellationRequested && deadlineSource.IsCancellationRequested)
        {
            var failedAt = DateTimeOffset.UtcNow;
            foreach (var workItem in workItems.Where(workItem =>
                         workItem.Entity.ProcessingStatus == ModerationProcessingStatus.Processing))
            {
                workItem.Entity.Fail("SYNC_DEADLINE_EXCEEDED", failedAt);
            }

            moderationRequest.Fail(failedAt);
            await moderationStore.SaveChangesAsync(CancellationToken.None);
            throw new RequestTimeoutException(
                $"同步审核超过 {executionPolicy.SynchronousDeadline.TotalSeconds:0} 秒截止时间，可使用 requestId 查询失败记录。");
        }

        return ModerationMappings.ToResponse(moderationRequest);
    }

    public async Task ProcessQueuedBatchAsync(Guid requestId, CancellationToken cancellationToken)
    {
        var request = await moderationStore.GetForProcessingAsync(requestId, cancellationToken)
            ?? throw new ResourceNotFoundException("审核批次不存在。");
        if (request.ProcessingStatus is ModerationProcessingStatus.Completed or
            ModerationProcessingStatus.CompletedWithErrors or
            ModerationProcessingStatus.Failed or
            ModerationProcessingStatus.Cancelled)
        {
            return;
        }

        var ruleSet = await ruleSetStore.GetByPublicRevisionIdAsync(
            request.PolicyRevision,
            cancellationToken)
            ?? throw new RequestConflictException("审核批次绑定的规则版本不可用。");
        if (ruleSet.Status != RuleSetStatus.Published)
        {
            throw new RequestConflictException("审核批次绑定的规则版本未发布。");
        }

        request.StartProcessing();
        var rules = ruleSet.Rules
            .Where(rule => rule.IsEnabled)
            .OrderByDescending(rule => rule.Weight)
            .ToArray();
        var normalizationOptions = RuleNormalizationOptions.ForProfile(ruleSet.NormalizationProfile);
        var compiledPolicy = ruleModerationEngine.GetOrCompile(
            ruleSet.PublicRevisionId,
            rules,
            ruleSet.RegexRules.Where(rule => rule.IsEnabled).ToArray(),
            ruleSet.CombinationRules.Where(rule => rule.IsEnabled).ToArray(),
            normalizationOptions);
        var workItems = request.Items
            .OrderBy(item => item.Ordinal)
            .Where(item => item.ProcessingStatus is not (
                ModerationProcessingStatus.Completed or
                ModerationProcessingStatus.Failed or
                ModerationProcessingStatus.Cancelled))
            .Select(item =>
            {
                item.StartProcessing();
                var content = contentProtector.Unprotect(item.Content);
                return new ModerationWorkItem(
                    item,
                    content,
                    compiledPolicy.Evaluate(content, item.Language, item.Scene),
                    normalizationOptions);
            })
            .ToArray();
        var processingJob = await moderationJobStore.GetByRequestIdAsync(
            request.ApplicationId,
            request.Id,
            cancellationToken);
        await ExecuteAsync(
            request,
            workItems,
            processingJob?.AttemptCount ?? 1,
            "async_worker",
            null,
            false,
            true,
            cancellationToken);
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

    public async Task FinalizeDeadLetterAsync(
        Guid requestId,
        CancellationToken cancellationToken)
    {
        var request = await moderationStore.GetForProcessingAsync(requestId, cancellationToken)
            ?? throw new ResourceNotFoundException("审核批次不存在。");
        if (request.ProcessingStatus is not (
                ModerationProcessingStatus.CompletedWithErrors or
                ModerationProcessingStatus.Failed))
        {
            throw new RequestConflictException("审核批次尚未进入可发布的失败终态。");
        }

        var eventType = request.ProcessingStatus == ModerationProcessingStatus.Failed
            ? "moderation.failed"
            : "moderation.completed";
        var payload = OperationalFactPayloads.Moderation(
            request,
            request.ProcessingStatus == ModerationProcessingStatus.Failed
                ? "failed"
                : "completed_with_errors",
            request.Items.Count,
            request.Items.LongCount(item => item.ProviderRequestId is not null),
            request.Items.LongCount(item => item.AiFailureCode is not null));
        await operationalFactService.EnqueueAsync(
            new OutboxMessage(
                eventType,
                "moderation_request",
                request.Id,
                request.TenantId,
                request.ApplicationId,
                payload,
                request.FinalizedAt ?? DateTimeOffset.UtcNow),
            cancellationToken);
        await webhookPublicationService.EnqueueModerationTerminalAsync(request, cancellationToken);
        await moderationStore.SaveChangesAsync(cancellationToken);
    }

    public async Task<BatchModerationResponse> CancelBatchAsync(
        Guid requestId,
        ApiKeyPrincipalData principal,
        string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        var requestStopwatch = Stopwatch.StartNew();
        var identity = ModerationCancellationIdentity.Create(
            principal.ApplicationId,
            requestId,
            idempotencyKey,
            idempotencyDigestService);
        var cancelledAt = DateTimeOffset.UtcNow;
        await using var transaction = await moderationCancellationStore.BeginAsync(cancellationToken);
        var job = await transaction.GetJobForUpdateAsync(
            principal.ApplicationId,
            requestId,
            cancellationToken);
        if (job is null)
        {
            await RecordCancellationRequestFactAsync(
                principal,
                requestId,
                itemCount: null,
                "new_idempotent",
                HttpNotFoundStatus,
                requestStopwatch,
                cancelledAt,
                cancellationToken);
            await transaction.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            throw new ResourceNotFoundException("异步审核批次不存在。");
        }

        var existingOperation = await transaction.GetOperationAsync(
            principal.ApplicationId,
            requestId,
            ModerationCancellationIdentity.Operation,
            identity.IdempotencyKeyDigest,
            cancelledAt,
            cancellationToken);
        if (existingOperation is not null)
        {
            if (!string.Equals(
                    existingOperation.OperationFingerprint,
                    identity.OperationFingerprint,
                    StringComparison.Ordinal))
            {
                await RecordCancellationRequestFactAsync(
                    principal,
                    requestId,
                    job.Request?.Items.Count,
                    "conflict",
                    HttpConflictStatus,
                    requestStopwatch,
                    cancelledAt,
                    cancellationToken);
                await transaction.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                throw new RequestConflictException("相同 Idempotency-Key 已用于不同的取消请求。");
            }

            await RecordCancellationRequestFactAsync(
                principal,
                requestId,
                job.Request?.Items.Count,
                "replay",
                existingOperation.HttpStatusCode,
                requestStopwatch,
                cancelledAt,
                cancellationToken);
            await transaction.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return JsonSerializer.Deserialize<BatchModerationResponse>(
                    existingOperation.ResponseSnapshot,
                    IdempotentResponseJsonOptions)
                ?? throw new InvalidOperationException("取消操作的幂等响应快照无效。");
        }

        if (job.Status is not (ModerationJobStatus.Pending or ModerationJobStatus.RetryWait) ||
            job.Request is null)
        {
            await RecordCancellationRequestFactAsync(
                principal,
                requestId,
                job.Request?.Items.Count,
                "conflict",
                HttpConflictStatus,
                requestStopwatch,
                cancelledAt,
                cancellationToken);
            await transaction.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            throw new RequestConflictException("审核批次已经开始或已终结，无法取消。");
        }

        job.Cancel(cancelledAt);
        job.Request.Cancel(cancelledAt);
        var response = ModerationMappings.ToResponse(job.Request);
        var responseSnapshot = JsonSerializer.Serialize(response, IdempotentResponseJsonOptions);
        var operation = new IdempotentOperation(
            job.Request.TenantId,
            job.Request.ApplicationId,
            job.Request.Id,
            ModerationCancellationIdentity.Operation,
            identity.IdempotencyKeyDigest,
            identity.OperationFingerprint,
            HttpOkStatus,
            responseSnapshot,
            cancelledAt,
            cancelledAt.Add(idempotencyPolicy.OperationRetention));
        await transaction.AddOperationAsync(operation, cancellationToken);
        await RecordCancellationRequestFactAsync(
            principal,
            requestId,
            job.Request.Items.Count,
            "new_idempotent",
            HttpOkStatus,
            requestStopwatch,
            cancelledAt,
            cancellationToken);
        var payload = OperationalFactPayloads.Moderation(
            job.Request,
            "cancelled",
            job.Request.Items.Count,
            0,
            0);
        await operationalFactService.EnqueueAsync(
            new OutboxMessage(
                "moderation.cancelled",
                "moderation_request",
                job.Request.Id,
                job.Request.TenantId,
                job.Request.ApplicationId,
                payload,
                cancelledAt),
            cancellationToken);
        await webhookPublicationService.EnqueueModerationTerminalAsync(
            job.Request,
            cancellationToken);
        await transaction.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return response;
    }

    private Task RecordCancellationRequestFactAsync(
        ApiKeyPrincipalData principal,
        Guid requestId,
        int? itemCount,
        string idempotencyOutcome,
        int httpStatusCode,
        Stopwatch requestStopwatch,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken)
    {
        return operationalFactService.RecordApiRequestAsync(
            new ApiRequestMeasurement(
                principal.TenantId,
                principal.ApplicationId,
                principal.KeyId,
                requestId,
                "/api/v1/moderation/batches/{requestId}/cancel",
                "authenticated",
                idempotencyOutcome,
                httpStatusCode,
                itemCount,
                Math.Max(0, requestStopwatch.ElapsedMilliseconds),
                occurredAt),
            cancellationToken);
    }

    private async Task ExecuteAsync(
        ModerationRequest request,
        IReadOnlyCollection<ModerationWorkItem> workItems,
        int attemptNumber,
        string idempotencyOutcome,
        Stopwatch? requestStopwatch,
        bool recordApiRequest,
        bool publishWebhook,
        CancellationToken cancellationToken)
    {
        await Parallel.ForEachAsync(
            workItems.Where(workItem => workItem.RuleEvaluation.RequiresAi),
            new ParallelOptions
            {
                MaxDegreeOfParallelism = executionPolicy.MaximumConcurrentAiCalls,
                CancellationToken = cancellationToken
            },
            async (workItem, itemCancellationToken) =>
            {
                workItem.AiStartedAt = DateTimeOffset.UtcNow;
                workItem.AiResult = await moderationAiClient.ModerateAsync(
                    new AiModerationRequest(
                        request.TenantId,
                        request.ApplicationId,
                        workItem.Content,
                        workItem.Entity.Language),
                    itemCancellationToken);
                workItem.AiCompletedAt = DateTimeOffset.UtcNow;
            });

        foreach (var workItem in workItems)
        {
            var aiResult = workItem.AiResult;
            var evaluation = aiResult is null
                ? workItem.RuleEvaluation
                : AiModerationMappings.ToEvaluation(
                    aiResult,
                    workItem.RuleEvaluation,
                    workItem.Content,
                    workItem.NormalizationOptions);
            workItem.Entity.Complete(
                evaluation.Decision,
                evaluation.ReviewSource,
                evaluation.Degraded,
                evaluation.RiskScore,
                evaluation.ScoreSource,
                evaluation.Route,
                JsonSerializer.Serialize(evaluation.ReasonCodes),
                JsonSerializer.Serialize(evaluation.Categories),
                JsonSerializer.Serialize(
                    evaluation.EvidenceDetails.Count > 0
                        ? (object)evaluation.EvidenceDetails
                        : evaluation.Evidence),
                DateTimeOffset.UtcNow,
                aiResult?.ConfigurationRevision,
                aiResult?.ProviderRequestId,
                aiResult?.InputTokens,
                aiResult?.OutputTokens,
                aiResult?.FailureCode);

            if (aiResult is not null &&
                workItem.AiStartedAt is { } aiStartedAt &&
                workItem.AiCompletedAt is { } aiCompletedAt)
            {
                await operationalFactService.RecordAiInvocationAsync(
                    new AiInvocationMeasurement(
                        request.TenantId,
                        request.ApplicationId,
                        request.CreatedByApiKeyId,
                        request.Id,
                        workItem.Entity.Id,
                        aiResult.Outcome.ToString(),
                        aiResult.ConfigurationRevision,
                        aiResult.ProviderRequestId,
                        attemptNumber,
                        aiResult.InputTokens,
                        aiResult.OutputTokens,
                        aiResult.FailureCode,
                        Math.Max(0, (long)(aiCompletedAt - aiStartedAt).TotalMilliseconds),
                        aiStartedAt,
                        aiCompletedAt),
                    cancellationToken);
            }
        }

        request.Complete(DateTimeOffset.UtcNow);
        if (recordApiRequest)
        {
            var completedAt = request.FinalizedAt ?? DateTimeOffset.UtcNow;
            await operationalFactService.RecordApiRequestAsync(
                new ApiRequestMeasurement(
                    request.TenantId,
                    request.ApplicationId,
                    request.CreatedByApiKeyId,
                    request.Id,
                    "/api/v1/moderation/batches",
                    "authenticated",
                    idempotencyOutcome,
                    HttpOkStatus,
                    request.Items.Count,
                    requestStopwatch is null
                        ? null
                        : Math.Max(0, requestStopwatch.ElapsedMilliseconds),
                    completedAt),
                cancellationToken);
        }

        var aiCallCount = workItems.LongCount(workItem => workItem.AiResult is not null);
        var aiFailureCount = workItems.LongCount(workItem =>
            workItem.AiResult?.FailureCode is not null);
        var moderationPayload = OperationalFactPayloads.Moderation(
            request,
            "completed",
            request.Items.Count,
            aiCallCount,
            aiFailureCount);
        await operationalFactService.EnqueueAsync(
            new OutboxMessage(
                "moderation.completed",
                "moderation_request",
                request.Id,
                request.TenantId,
                request.ApplicationId,
                moderationPayload,
                request.FinalizedAt ?? DateTimeOffset.UtcNow),
            cancellationToken);
        if (publishWebhook)
        {
            await webhookPublicationService.EnqueueModerationTerminalAsync(
                request,
                cancellationToken);
        }
        await moderationStore.SaveChangesAsync(cancellationToken);
    }

    private async Task<RuleSetVersion> GetPublishedRuleSetAsync(
        Guid applicationId,
        string? requestedPolicyId,
        CancellationToken cancellationToken)
    {
        var ruleSet = await ruleSetStore.GetBoundForApplicationAsync(applicationId, cancellationToken);
        if (ruleSet is null || ruleSet.Status != RuleSetStatus.Published)
        {
            throw new RequestConflictException("应用尚未绑定可用的已发布规则集。");
        }

        if (!string.IsNullOrWhiteSpace(requestedPolicyId) &&
            !string.Equals(requestedPolicyId.Trim(), ruleSet.PublicRevisionId, StringComparison.Ordinal))
        {
            throw new RequestConflictException("请求策略与应用当前绑定的规则集不一致。");
        }

        return ruleSet;
    }

    private bool ShouldEnqueue(
        BatchModerationRequest request,
        IReadOnlyCollection<RuleEvaluation> evaluations)
    {
        return request.Mode == ModerationMode.Async ||
               request.Mode == ModerationMode.Auto &&
               (request.Items.Count > queuePolicy.AutoAsyncItemThreshold ||
                evaluations.Count(evaluation => evaluation.RequiresAi) > queuePolicy.AutoAsyncAiThreshold);
    }

    private void ValidateRequest(BatchModerationRequest request)
    {
        var maximumItems = request.Mode == ModerationMode.Sync
            ? queuePolicy.MaximumSyncItems
            : queuePolicy.MaximumAsyncItems;
        if (request.Items.Count == 0 || request.Items.Count > maximumItems)
        {
            throw new RequestValidationException(
                request.Mode == ModerationMode.Sync
                    ? $"同步审核内容数量必须在 1 到 {queuePolicy.MaximumSyncItems} 条之间。"
                    : $"异步或自动审核内容数量必须在 1 到 {queuePolicy.MaximumAsyncItems} 条之间。");
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
                throw new RequestValidationException("单条内容超过 64 KiB 限制。");
            }

            if (!string.Equals(item.ContentType, "plain_text", StringComparison.OrdinalIgnoreCase))
            {
                throw new RequestValidationException("当前版本只支持纯文本内容。");
            }
        }
    }

    private async Task<BatchModerationResponse> ResolveReplayAsync(
        ModerationRequest existing,
        string requestFingerprint,
        ApiKeyPrincipalData principal,
        int requestedItemCount,
        string replayOutcome,
        Stopwatch requestStopwatch,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(existing.RequestFingerprint, requestFingerprint, StringComparison.Ordinal))
        {
            await RecordSubmissionFactAsync(
                existing,
                principal,
                requestedItemCount,
                "conflict",
                HttpConflictStatus,
                requestStopwatch,
                "conflict",
                cancellationToken);
            throw new RequestConflictException("相同幂等键已用于不同的审核请求。");
        }

        await RecordSubmissionFactAsync(
            existing,
            principal,
            existing.Items.Count,
            replayOutcome,
            IsTerminal(existing.ProcessingStatus) ? HttpOkStatus : HttpAcceptedStatus,
            requestStopwatch,
            "replay",
            cancellationToken);

        return ModerationMappings.ToResponse(existing);
    }

    private static bool IsTerminal(ModerationProcessingStatus status)
    {
        return status is ModerationProcessingStatus.Completed or
            ModerationProcessingStatus.CompletedWithErrors or
            ModerationProcessingStatus.Failed or
            ModerationProcessingStatus.Cancelled;
    }

    private async Task RecordSubmissionFactAsync(
        ModerationRequest request,
        ApiKeyPrincipalData principal,
        int itemCount,
        string idempotencyOutcome,
        int statusCode,
        Stopwatch? requestStopwatch,
        string eventAction,
        CancellationToken cancellationToken)
    {
        var occurredAt = DateTimeOffset.UtcNow;
        await operationalFactService.RecordApiRequestAsync(
            new ApiRequestMeasurement(
                request.TenantId,
                request.ApplicationId,
                principal.KeyId,
                request.Id,
                "/api/v1/moderation/batches",
                "authenticated",
                idempotencyOutcome,
                statusCode,
                itemCount,
                requestStopwatch is null
                    ? null
                    : Math.Max(0, requestStopwatch.ElapsedMilliseconds),
                occurredAt),
            cancellationToken);
        if (eventAction == "accepted")
        {
            var payload = OperationalFactPayloads.Moderation(
                request,
                eventAction,
                itemCount,
                0,
                0);
            await operationalFactService.EnqueueAsync(
                new OutboxMessage(
                    "moderation.accepted",
                    "moderation_request",
                    request.Id,
                    request.TenantId,
                    request.ApplicationId,
                    payload,
                    occurredAt),
                cancellationToken);
        }

        await moderationStore.SaveChangesAsync(cancellationToken);
    }

    private sealed class ModerationWorkItem(
        ModerationItem entity,
        string content,
        RuleEvaluation ruleEvaluation,
        RuleNormalizationOptions normalizationOptions)
    {
        public ModerationItem Entity { get; } = entity;

        public string Content { get; } = content;

        public RuleEvaluation RuleEvaluation { get; } = ruleEvaluation;

        public RuleNormalizationOptions NormalizationOptions { get; } = normalizationOptions;

        public AiModerationResult? AiResult { get; set; }

        public DateTimeOffset? AiStartedAt { get; set; }

        public DateTimeOffset? AiCompletedAt { get; set; }
    }
}
