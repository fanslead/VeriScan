using VeriScan.Application.Abstractions;
using VeriScan.Application.Contracts;

namespace VeriScan.Application.Services;

/// <summary>管理端应用用量查询服务。</summary>
public interface IApplicationUsageService
{
    Task<ApplicationUsageResponse> GetAsync(
        Guid applicationId,
        ApplicationUsageQuery query,
        CancellationToken cancellationToken);
}

public sealed class ApplicationUsageService(IApplicationUsageStore usageStore) : IApplicationUsageService
{
    private static readonly TimeSpan DefaultWindow = TimeSpan.FromDays(7);
    private static readonly TimeSpan MaximumWindow = TimeSpan.FromDays(90);

    public async Task<ApplicationUsageResponse> GetAsync(
        Guid applicationId,
        ApplicationUsageQuery query,
        CancellationToken cancellationToken)
    {
        var through = (query.Through ?? DateTimeOffset.UtcNow).ToUniversalTime();
        var from = (query.From ?? through.Subtract(DefaultWindow)).ToUniversalTime();
        ValidateWindow(from, through);

        if (!await usageStore.ApplicationExistsAsync(applicationId, cancellationToken))
        {
            throw new ResourceNotFoundException("应用不存在。");
        }

        if (query.ApiKeyId is { } apiKeyId &&
            !await usageStore.ApiKeyBelongsToApplicationAsync(
                applicationId,
                apiKeyId,
                cancellationToken))
        {
            throw new ResourceNotFoundException("API Key 不属于该应用。");
        }

        var usage = await usageStore.GetAsync(
            applicationId,
            query.ApiKeyId,
            from,
            through,
            cancellationToken);

        return new ApplicationUsageResponse(
            applicationId,
            query.ApiKeyId,
            from,
            through,
            usage.RequestCount,
            usage.ItemCount,
            usage.PassCount,
            usage.RejectCount,
            usage.ReviewCount,
            usage.AiCallCount,
            usage.AiInputTokens,
            usage.AiOutputTokens,
            usage.AiFailureCount);
    }

    private static void ValidateWindow(DateTimeOffset from, DateTimeOffset through)
    {
        if (from >= through)
        {
            throw new RequestValidationException("统计窗口必须满足 from 早于 through。");
        }

        if (through - from > MaximumWindow)
        {
            throw new RequestValidationException("统计窗口不能超过 90 天。");
        }
    }
}
