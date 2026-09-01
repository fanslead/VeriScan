using System.Security.Cryptography;
using System.Text;
using VeriScan.Infrastructure.Security;

namespace VeriScan.Api.Tests;

public sealed class ModerationDigestTests
{
    private static readonly ModerationDigestOptions Options = new()
    {
        ContentPepper = "content-pepper-with-at-least-32-bytes-0001",
        IdempotencyPepper = "idempotency-pepper-with-at-least-32-bytes-0002",
        KeyVersion = "test-v1"
    };

    [Fact]
    public void SensitiveContentUsesKeyedDigestInsteadOfPlainSha256()
    {
        const string content = "短敏感文本";
        var service = new ContentHashService(Microsoft.Extensions.Options.Options.Create(Options));
        var plainHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content)))
            .ToLowerInvariant();

        var digest = service.Compute(content);

        Assert.Equal(64, digest.Length);
        Assert.NotEqual(plainHash, digest);
    }

    [Fact]
    public void ContentAndIdempotencyPurposesProduceDifferentDigests()
    {
        const string value = "same-input";
        var content = new ContentHashService(Microsoft.Extensions.Options.Options.Create(Options));
        var idempotency = new IdempotencyDigestService(
            Microsoft.Extensions.Options.Options.Create(Options));

        Assert.NotEqual(content.Compute(value), idempotency.Compute(value));
    }
}
