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
        int ordinal,
        string clientItemId,
        string content,
        string contentHash,
        string contentHashKeyVersion,
        string? language,
        string contentType,
        string? scene,
        string? authorType,
        DateTimeOffset createdAt,
        ModerationProcessingStatus initialStatus = ModerationProcessingStatus.Processing)
    {
        Id = Guid.CreateVersion7();
        RequestId = requestId;
        TenantId = tenantId;
        ApplicationId = applicationId;
        Ordinal = ordinal;
        ClientItemId = clientItemId;
        Content = content;
        ContentHash = contentHash;
        ContentHashKeyVersion = contentHashKeyVersion;
        Language = language;
        ContentType = contentType;
        Scene = scene;
        AuthorType = authorType;
        ProcessingStatus = initialStatus;
        Route = "rules";
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public Guid RequestId { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid ApplicationId { get; private set; }

    public int Ordinal { get; private set; }

    public string ClientItemId { get; private set; } = string.Empty;

    public string Content { get; private set; } = string.Empty;

    public string ContentHash { get; private set; } = string.Empty;

    public string ContentHashKeyVersion { get; private set; } = string.Empty;

    public string? Language { get; private set; }

    public string ContentType { get; private set; } = string.Empty;

    public string? Scene { get; private set; }

    public string? AuthorType { get; private set; }

    public ModerationProcessingStatus ProcessingStatus { get; private set; }

    public ModerationDecision? Decision { get; private set; }

    public string? ReviewSource { get; private set; }

    public bool Degraded { get; private set; }

    public decimal? RiskScore { get; private set; }

    public string? ScoreSource { get; private set; }

    public string Route { get; private set; } = string.Empty;

    public string ReasonCodesText { get; private set; } = "[]";

    public string CategoriesText { get; private set; } = "[]";

    public string EvidenceText { get; private set; } = "[]";

    public string? AiConfigurationRevision { get; private set; }

    public string? ProviderRequestId { get; private set; }

    public int? AiInputTokens { get; private set; }

    public int? AiOutputTokens { get; private set; }

    public string? AiFailureCode { get; private set; }

    public string? ErrorCode { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? MachineCompletedAt { get; private set; }

    public DateTimeOffset? FinalizedAt { get; private set; }

    public ModerationRequest? Request { get; private set; }

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
        if (ProcessingStatus is ModerationProcessingStatus.Accepted or ModerationProcessingStatus.RetryWait)
        {
            ProcessingStatus = ModerationProcessingStatus.Cancelled;
            FinalizedAt = cancelledAt;
        }
    }

    public void Complete(
        ModerationDecision decision,
        string? reviewSource,
        bool degraded,
        decimal? riskScore,
        string? scoreSource,
        string route,
        string reasonCodesText,
        string categoriesText,
        string evidenceText,
        DateTimeOffset completedAt,
        string? aiConfigurationRevision = null,
        string? providerRequestId = null,
        int? aiInputTokens = null,
        int? aiOutputTokens = null,
        string? aiFailureCode = null)
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
        EvidenceText = evidenceText;
        AiConfigurationRevision = aiConfigurationRevision;
        ProviderRequestId = providerRequestId;
        AiInputTokens = aiInputTokens;
        AiOutputTokens = aiOutputTokens;
        AiFailureCode = aiFailureCode;
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
