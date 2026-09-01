using System.Globalization;
using System.Text;
using VeriScan.Application.Abstractions;
using VeriScan.Application.Contracts;

namespace VeriScan.Application.Services;

internal sealed record ModerationRequestIdentity(
    string? IdempotencyKeyDigest,
    string RequestFingerprint)
{
    private const int MinimumKeyLength = 8;
    private const int MaximumKeyLength = 128;

    public static ModerationRequestIdentity Create(
        Guid applicationId,
        string? idempotencyKey,
        BatchModerationRequest request,
        string effectivePolicyId,
        IContentHashService contentHashService)
    {
        var normalizedKey = ValidateKey(idempotencyKey);
        var digest = normalizedKey is null
            ? null
            : contentHashService.Compute($"{applicationId:N}\0{normalizedKey}");
        return new ModerationRequestIdentity(
            digest,
            ComputeFingerprint(request, effectivePolicyId, contentHashService));
    }

    private static string? ValidateKey(string? idempotencyKey)
    {
        if (idempotencyKey is null)
        {
            return null;
        }

        if (idempotencyKey.Length is < MinimumKeyLength or > MaximumKeyLength ||
            idempotencyKey.Any(character =>
                !char.IsAsciiLetterOrDigit(character) && character is not '.' and not '_' and not ':' and not '-'))
        {
            throw new RequestValidationException(
                "Idempotency-Key 必须为 8 到 128 位，仅可包含 ASCII 字母、数字、点、下划线、冒号或连字符。");
        }

        return idempotencyKey;
    }

    private static string ComputeFingerprint(
        BatchModerationRequest request,
        string effectivePolicyId,
        IContentHashService contentHashService)
    {
        var canonical = new StringBuilder(capacity: 256);
        Append(canonical, request.Mode.ToString().ToLowerInvariant());
        Append(canonical, effectivePolicyId);
        canonical.Append(request.Items.Count.ToString(CultureInfo.InvariantCulture)).Append(':');
        foreach (var item in request.Items)
        {
            Append(canonical, item.Id);
            Append(canonical, item.Content);
            Append(canonical, item.Language ?? string.Empty);
            Append(canonical, item.ContentType.ToLowerInvariant());
        }

        return contentHashService.Compute(canonical.ToString());
    }

    private static void Append(StringBuilder target, string value)
    {
        target.Append(value.Length.ToString(CultureInfo.InvariantCulture))
            .Append(':')
            .Append(value);
    }
}
