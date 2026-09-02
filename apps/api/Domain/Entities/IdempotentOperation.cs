namespace VeriScan.Domain.Entities;

/// <summary>记录关键写操作的幂等结果，不保存调用方提供的原始幂等键。</summary>
public sealed class IdempotentOperation
{
    private IdempotentOperation()
    {
    }

    public IdempotentOperation(
        Guid tenantId,
        Guid applicationId,
        Guid targetRequestId,
        string operation,
        string idempotencyKeyDigest,
        string operationFingerprint,
        int httpStatusCode,
        string responseSnapshot,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt)
    {
        if (expiresAt <= createdAt)
        {
            throw new ArgumentOutOfRangeException(nameof(expiresAt), "幂等操作过期时间必须晚于创建时间。");
        }

        Id = Guid.CreateVersion7();
        TenantId = tenantId;
        ApplicationId = applicationId;
        TargetRequestId = targetRequestId;
        Operation = operation;
        IdempotencyKeyDigest = idempotencyKeyDigest;
        OperationFingerprint = operationFingerprint;
        HttpStatusCode = httpStatusCode;
        ResponseSnapshot = responseSnapshot;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid ApplicationId { get; private set; }

    public Guid TargetRequestId { get; private set; }

    public string Operation { get; private set; } = string.Empty;

    public string IdempotencyKeyDigest { get; private set; } = string.Empty;

    public string OperationFingerprint { get; private set; } = string.Empty;

    public int HttpStatusCode { get; private set; }

    /// <summary>保存首次成功响应的安全快照，用于后续精确重放。</summary>
    public string ResponseSnapshot { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }
}
