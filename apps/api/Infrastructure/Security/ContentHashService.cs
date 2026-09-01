using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using VeriScan.Application.Abstractions;

namespace VeriScan.Infrastructure.Security;

public sealed class ContentHashService(IOptions<ModerationDigestOptions> options) : IContentHashService
{
    private readonly byte[] pepper = Encoding.UTF8.GetBytes(options.Value.ContentPepper);

    public string KeyVersion { get; } = options.Value.KeyVersion;

    public string Compute(string content)
    {
        return Convert.ToHexString(
            HMACSHA256.HashData(pepper, Encoding.UTF8.GetBytes(content)))
            .ToLowerInvariant();
    }
}

public sealed class IdempotencyDigestService(IOptions<ModerationDigestOptions> options)
    : IIdempotencyDigestService
{
    private readonly byte[] pepper = Encoding.UTF8.GetBytes(options.Value.IdempotencyPepper);

    public string Compute(string value)
    {
        return Convert.ToHexString(
            HMACSHA256.HashData(pepper, Encoding.UTF8.GetBytes(value)))
            .ToLowerInvariant();
    }
}
