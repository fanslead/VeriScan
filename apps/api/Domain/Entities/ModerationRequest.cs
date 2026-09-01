namespace VeriScan.Domain.Entities;

public sealed class ModerationRequest
{
    private ModerationRequest()
    {
    }

    public ModerationRequest(
        Guid tenantId,
        Guid applicationId,
        Guid createdByApiKeyId,
        string mode,
        string policyRevision,
        string? idempotencyKeyDigest,
        string? requestFingerprint,
        DateTimeOffset submittedAt,
        ModerationProcessingStatus initialStatus = ModerationProcessingStatus.Processing)
    {
        Id = Guid.CreateVersion7();
        TenantId = tenantId;
        ApplicationId = applicationId;
        CreatedByApiKeyId = createdByApiKeyId;
        Mode = mode;
        PolicyRevision = policyRevision;
        IdempotencyKeyDigest = idempotencyKeyDigest;
        RequestFingerprint = requestFingerprint;
        ProcessingStatus = initialStatus;
        SubmittedAt = submittedAt;
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid ApplicationId { get; private set; }

    public Guid CreatedByApiKeyId { get; private set; }

    public string Mode { get; private set; } = string.Empty;

    public string PolicyRevision { get; private set; } = string.Empty;

    public string? IdempotencyKeyDigest { get; private set; }

    public string? RequestFingerprint { get; private set; }

    public ModerationProcessingStatus ProcessingStatus { get; private set; }

    public DateTimeOffset SubmittedAt { get; private set; }

    public DateTimeOffset? MachineCompletedAt { get; private set; }

    public DateTimeOffset? FinalizedAt { get; private set; }

    public ICollection<ModerationItem> Items { get; private set; } = new List<ModerationItem>();

    public ApplicationEntity? Application { get; private set; }

    public void AddItem(ModerationItem item)
    {
        Items.Add(item);
    }

    public void StartProcessing()
    {
        if (ProcessingStatus is ModerationProcessingStatus.Accepted or ModerationProcessingStatus.RetryWait)
        {
            ProcessingStatus = ModerationProcessingStatus.Processing;
        }
    }

    public void MarkRetryWait()
    {
        if (ProcessingStatus == ModerationProcessingStatus.Processing)
        {
            ProcessingStatus = ModerationProcessingStatus.RetryWait;
        }
    }

    public void Cancel(DateTimeOffset cancelledAt)
    {
        if (ProcessingStatus is not (ModerationProcessingStatus.Accepted or ModerationProcessingStatus.RetryWait))
        {
            throw new InvalidOperationException("只有尚未开始的审核批次可以取消。");
        }

        foreach (var item in Items)
        {
            item.Cancel(cancelledAt);
        }

        ProcessingStatus = ModerationProcessingStatus.Cancelled;
        FinalizedAt = cancelledAt;
    }

    public void Fail(DateTimeOffset failedAt)
    {
        ProcessingStatus = ModerationProcessingStatus.Failed;
        MachineCompletedAt = failedAt;
        FinalizedAt = failedAt;
    }

    public void Complete(DateTimeOffset completedAt)
    {
        MachineCompletedAt = completedAt;
        FinalizedAt = completedAt;
        var hasCompleted = Items.Any(item => item.ProcessingStatus == ModerationProcessingStatus.Completed);
        var hasError = Items.Any(item => item.ProcessingStatus is ModerationProcessingStatus.Failed or ModerationProcessingStatus.Cancelled);
        ProcessingStatus = hasCompleted && hasError
            ? ModerationProcessingStatus.CompletedWithErrors
            : hasCompleted
                ? ModerationProcessingStatus.Completed
                : Items.All(item => item.ProcessingStatus == ModerationProcessingStatus.Cancelled)
                    ? ModerationProcessingStatus.Cancelled
                    : ModerationProcessingStatus.Failed;
    }
}

public enum ModerationProcessingStatus
{
    Accepted,
    Processing,
    RetryWait,
    Completed,
    CompletedWithErrors,
    Failed,
    Cancelled
}
