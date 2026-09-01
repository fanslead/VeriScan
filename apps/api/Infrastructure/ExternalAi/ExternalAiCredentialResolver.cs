using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;

namespace VeriScan.Infrastructure.ExternalAi;

public interface IExternalAiCredentialResolver
{
    bool TryResolve(string credentialReference, out string credential);
}

public sealed partial class ExternalAiCredentialResolver(IConfiguration configuration) : IExternalAiCredentialResolver
{
    public bool TryResolve(string credentialReference, out string credential)
    {
        credential = string.Empty;
        if (CredentialReferencePattern().Match(credentialReference.Trim()) is not { Success: true } match)
        {
            return false;
        }

        var name = match.Groups["name"].Value;
        var configuredCredential = configuration[$"{ExternalAiOptions.SectionName}:Credentials:{name}"];
        if (string.IsNullOrWhiteSpace(configuredCredential))
        {
            return false;
        }

        credential = configuredCredential;
        return true;
    }

    [GeneratedRegex("^config://(?<name>[A-Za-z][A-Za-z0-9_.-]{0,127})$", RegexOptions.CultureInvariant)]
    private static partial Regex CredentialReferencePattern();
}
