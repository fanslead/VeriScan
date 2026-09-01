namespace VeriScan.Domain.Entities;

public sealed class ModerationJob
{
    private ModerationJob()
    {
    }

    public ModerationJob(
        Guid tenantId,
        Guid applicationId,
        Guid requestId,
        int priority,
        int maximumAttempts,
        DateTimeOffset createdAt)
    {
        if (maximumAttempts is < 1 or > 20)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumAttempts));
        }

        Id = Guid.CreateVersion7();
        TenantId = tenantId;
        ApplicationId = applicationId;
        RequestId = requestId;
        Priority = priority;
        MaximumAttempts = maximumAttempts;
        Status = ModerationJobStatus.Pending;
        AvailableAt = createdAt;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid ApplicationId { get; private set; }

    public Guid RequestId { get; private set; }

    public int Priority { get; private set; }

    public ModerationJobStatus Status { get; private set; }

    public int AttemptCount { get; private set; }

    public int MaximumAttempts { get; private set; }

    public DateTimeOffset AvailableAt { get; private set; }

    public string? LeaseOwner { get; private set; }

    public DateTimeOffset? LeaseExpiresAt { get; private set; }

    public string? LastErrorCode { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public ModerationRequest? Request { get; private set; }

    public bool CanBeClaimed(DateTimeOffset now) =>
        Status is ModerationJobStatus.Pending or ModerationJobStatus.RetryWait ||
        Status == ModerationJobStatus.Processing && LeaseExpiresAt <= now;

    public void Claim(string leaseOwner, DateTimeOffset now, TimeSpan leaseDuration)
    {
        if (!CanBeClaimed(now) || AvailableAt > now)
        {
            throw new InvalidOperationException("审核任务当前不可领取。");
        }

        AttemptCount++;
        Status = ModerationJobStatus.Processing;
        LeaseOwner = leaseOwner;
        LeaseExpiresAt = now.Add(leaseDuration);
        UpdatedAt = now;
    }

    public void Complete(DateTimeOffset completedAt)
    {
        Status = ModerationJobStatus.Completed;
        LeaseOwner = null;
        LeaseExpiresAt = null;
        LastErrorCode = null;
        UpdatedAt = completedAt;
    }

    public void Retry(string errorCode, DateTimeOffset now, TimeSpan delay)
    {
        LastErrorCode = errorCode;
        LeaseOwner = null;
        LeaseExpiresAt = null;
        UpdatedAt = now;
        if (AttemptCount >= MaximumAttempts)
        {
            Status = ModerationJobStatus.DeadLetter;
            return;
        }

        Status = ModerationJobStatus.RetryWait;
        AvailableAt = now.Add(delay);
    }

    public void Cancel(DateTimeOffset cancelledAt)
    {
        if (Status == ModerationJobStatus.Completed)
        {
            throw new InvalidOperationException("已完成的审核任务不能取消。");
        }

        Status = ModerationJobStatus.Cancelled;
        LeaseOwner = null;
        LeaseExpiresAt = null;
        UpdatedAt = cancelledAt;
    }
}

public enum ModerationJobStatus
{
    Pending,
    Processing,
    RetryWait,
    Completed,
    DeadLetter,
    Cancelled
}
