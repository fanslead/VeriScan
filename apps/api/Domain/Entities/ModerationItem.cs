namespace VeriScan.Domain.Entities;

public sealed class ModerationItem
{
    private ModerationItem()
    {
    }

    public ModerationItem(
        Guid requestId,
        Guid tenantId,
        Guid applicationId,
        string clientItemId,
        string content,
        string contentHash,
        string? language,
        string contentType,
        DateTimeOffset createdAt)
    {
        Id = Guid.CreateVersion7();
        RequestId = requestId;
        TenantId = tenantId;
        ApplicationId = applicationId;
        ClientItemId = clientItemId;
        Content = content;
        ContentHash = contentHash;
        Language = language;
        ContentType = contentType;
        ProcessingStatus = ModerationProcessingStatus.Processing;
        Route = "rules";
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public Guid RequestId { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid ApplicationId { get; private set; }

    public string ClientItemId { get; private set; } = string.Empty;

    public string Content { get; private set; } = string.Empty;

    public string ContentHash { get; private set; } = string.Empty;

    public string? Language { get; private set; }

    public string ContentType { get; private set; } = string.Empty;

    public ModerationProcessingStatus ProcessingStatus { get; private set; }

    public ModerationDecision? Decision { get; private set; }

    public string? ReviewSource { get; private set; }

    public bool Degraded { get; private set; }

    public decimal? RiskScore { get; private set; }

    public string? ScoreSource { get; private set; }

    public string Route { get; private set; } = string.Empty;

    public string ReasonCodesText { get; private set; } = string.Empty;

    public string CategoriesText { get; private set; } = string.Empty;

    public string? ErrorCode { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? MachineCompletedAt { get; private set; }

    public DateTimeOffset? FinalizedAt { get; private set; }

    public ModerationRequest? Request { get; private set; }

    public void Complete(
        ModerationDecision decision,
        string? reviewSource,
        bool degraded,
        decimal? riskScore,
        string? scoreSource,
        string route,
        string reasonCodesText,
        string categoriesText,
        DateTimeOffset completedAt)
    {
        ProcessingStatus = ModerationProcessingStatus.Completed;
        Decision = decision;
        ReviewSource = reviewSource;
        Degraded = degraded;
        RiskScore = riskScore;
        ScoreSource = scoreSource;
        Route = route;
        ReasonCodesText = reasonCodesText;
        CategoriesText = categoriesText;
        MachineCompletedAt = completedAt;
        FinalizedAt = completedAt;
    }

    public void Fail(string errorCode, DateTimeOffset completedAt)
    {
        ProcessingStatus = ModerationProcessingStatus.Failed;
        ErrorCode = errorCode;
        MachineCompletedAt = completedAt;
        FinalizedAt = completedAt;
    }
}

public enum ModerationDecision
{
    Pass,
    Reject,
    Review
}
