using System.Security.Cryptography;
using System.Text;
using VeriScan.Application.Abstractions;

namespace VeriScan.Infrastructure.Security;

public sealed class ContentHashService : IContentHashService
{
    public string Compute(string content)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content))).ToLowerInvariant();
    }
}
