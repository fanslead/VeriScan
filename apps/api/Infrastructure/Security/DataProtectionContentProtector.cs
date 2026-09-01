using Microsoft.AspNetCore.DataProtection;
using VeriScan.Application.Abstractions;

namespace VeriScan.Infrastructure.Security;

public sealed class DataProtectionContentProtector(IDataProtectionProvider provider)
    : IModerationContentProtector
{
    private const string Prefix = "dp:v1:";
    private readonly IDataProtector protector = provider.CreateProtector(
        "VeriScan",
        "ModerationContent",
        "v1");

    public string Protect(string plaintext)
    {
        ArgumentNullException.ThrowIfNull(plaintext);
        return Prefix + protector.Protect(plaintext);
    }

    public string Unprotect(string protectedContent)
    {
        ArgumentNullException.ThrowIfNull(protectedContent);
        if (!protectedContent.StartsWith(Prefix, StringComparison.Ordinal))
        {
            // 仅兼容升级前的本地历史记录，新写入内容始终带保护版本前缀。
            return protectedContent;
        }

        return protector.Unprotect(protectedContent[Prefix.Length..]);
    }
}
