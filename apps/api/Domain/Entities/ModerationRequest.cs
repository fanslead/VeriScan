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
        DateTimeOffset submittedAt)
    {
        Id = Guid.CreateVersion7();
        TenantId = tenantId;
        ApplicationId = applicationId;
        CreatedByApiKeyId = createdByApiKeyId;
        Mode = mode;
        PolicyRevision = policyRevision;
        IdempotencyKeyDigest = idempotencyKeyDigest;
        RequestFingerprint = requestFingerprint;
        ProcessingStatus = ModerationProcessingStatus.Processing;
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

    public void Complete(DateTimeOffset completedAt)
    {
        MachineCompletedAt = completedAt;
        FinalizedAt = completedAt;
        ProcessingStatus = Items.Any(item => item.ProcessingStatus == ModerationProcessingStatus.Failed)
            ? ModerationProcessingStatus.CompletedWithErrors
            : ModerationProcessingStatus.Completed;
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
