using System.Globalization;
using System.Text;
using VeriScan.Application.Abstractions;
using VeriScan.Application.Contracts;

namespace VeriScan.Application.Services;

internal sealed record ModerationRequestIdentity(
    string? IdempotencyKeyDigest,
    string RequestFingerprint)
{
    public static ModerationRequestIdentity Create(
        Guid applicationId,
        string? idempotencyKey,
        BatchModerationRequest request,
        string effectivePolicyId,
        IIdempotencyDigestService digestService)
    {
        var normalizedKey = IdempotencyKeyPolicy.ValidateOptional(idempotencyKey);
        var digest = normalizedKey is null
            ? null
            : digestService.Compute($"{applicationId:N}\0{normalizedKey}");
        return new ModerationRequestIdentity(
            digest,
            ComputeFingerprint(request, effectivePolicyId, digestService));
    }

    private static string ComputeFingerprint(
        BatchModerationRequest request,
        string effectivePolicyId,
        IIdempotencyDigestService digestService)
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
            Append(canonical, item.Context?.Scene ?? string.Empty);
            Append(canonical, item.Context?.AuthorType ?? string.Empty);
        }

        return digestService.Compute(canonical.ToString());
    }

    private static void Append(StringBuilder target, string value)
    {
        target.Append(value.Length.ToString(CultureInfo.InvariantCulture))
            .Append(':')
            .Append(value);
    }
}
