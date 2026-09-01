namespace VeriScan.Application.Abstractions;

/// <summary>应用用量只读事实存储。</summary>
public interface IApplicationUsageStore
{
    Task<bool> ApplicationExistsAsync(
        Guid applicationId,
        CancellationToken cancellationToken);

    Task<bool> ApiKeyBelongsToApplicationAsync(
        Guid applicationId,
        Guid apiKeyId,
        CancellationToken cancellationToken);

    Task<ApplicationUsageReadData> GetAsync(
        Guid applicationId,
        Guid? apiKeyId,
        DateTimeOffset from,
        DateTimeOffset through,
        CancellationToken cancellationToken);
}

/// <summary>从审核请求和审核内容事实表读取的应用用量。</summary>
public sealed record ApplicationUsageReadData(
    long RequestCount,
    long ItemCount,
    long PassCount,
    long RejectCount,
    long ReviewCount,
    long AiCallCount,
    long? AiInputTokens,
    long? AiOutputTokens,
    long AiFailureCount);
