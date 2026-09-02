using VeriScan.Application.Abstractions;

namespace VeriScan.Application.Services;

/// <summary>取消操作独立的幂等命名空间与请求指纹。</summary>
internal sealed record ModerationCancellationIdentity(
    string IdempotencyKeyDigest,
    string OperationFingerprint)
{
    public const string Operation = "cancel";

    public static ModerationCancellationIdentity Create(
        Guid applicationId,
        Guid requestId,
        string? idempotencyKey,
        IIdempotencyDigestService digestService)
    {
        var normalizedKey = IdempotencyKeyPolicy.ValidateRequired(idempotencyKey);
        var digest = digestService.Compute(
            $"{applicationId:N}\0{Operation}\0{normalizedKey}");
        var fingerprint = digestService.Compute(
            $"v1\0{applicationId:N}\0{requestId:N}\0{Operation}");
        return new ModerationCancellationIdentity(digest, fingerprint);
    }
}
