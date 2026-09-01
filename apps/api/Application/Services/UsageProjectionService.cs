using VeriScan.Application.Abstractions;
using VeriScan.Application.Contracts;

namespace VeriScan.Application.Services;

/// <summary>校验窗口后按数据库事实重建小时和日用量投影。</summary>
public sealed class UsageProjectionService(IUsageProjectionStore store) : IUsageProjectionService
{
    private static readonly TimeSpan DefaultWindow = TimeSpan.FromDays(7);
    private static readonly TimeSpan MaximumWindow = TimeSpan.FromDays(90);

    public async Task<UsageRebuildData> RebuildAsync(
        Guid applicationId,
        Guid? apiKeyId,
        DateTimeOffset? from,
        DateTimeOffset? through,
        CancellationToken cancellationToken)
    {
        var dataThrough = (through ?? DateTimeOffset.UtcNow).ToUniversalTime();
        var dataFrom = (from ?? dataThrough.Subtract(DefaultWindow)).ToUniversalTime();
        if (dataFrom >= dataThrough)
        {
            throw new RequestValidationException("统计窗口必须满足 from 早于 through。");
        }

        if (dataThrough - dataFrom > MaximumWindow)
        {
            throw new RequestValidationException("统计窗口不能超过 90 天。");
        }

        if (!await store.ApplicationExistsAsync(applicationId, cancellationToken))
        {
            throw new ResourceNotFoundException("应用不存在。");
        }

        if (apiKeyId is { } keyId &&
            !await store.ApiKeyBelongsToApplicationAsync(
                applicationId,
                keyId,
                cancellationToken))
        {
            throw new ResourceNotFoundException("API Key 不属于该应用。");
        }

        return await store.RebuildAsync(
            applicationId,
            apiKeyId,
            dataFrom,
            dataThrough,
            cancellationToken);
    }
}
