using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using VeriScan.Application.Abstractions;
using VeriScan.Domain.Entities;

namespace VeriScan.Infrastructure.ExternalAi;

public interface IExternalAiCredentialResolver
{
    bool TryResolve(AiModelConfiguration configuration, out string credential);
}

public sealed partial class ExternalAiCredentialResolver(
    IConfiguration appConfiguration,
    IAiCredentialProtector credentialProtector) : IExternalAiCredentialResolver
{
    public bool TryResolve(AiModelConfiguration configuration, out string credential)
    {
        credential = string.Empty;
        if (!string.IsNullOrWhiteSpace(configuration.CredentialCiphertext))
        {
            return credentialProtector.TryUnprotect(configuration.CredentialCiphertext, out credential);
        }

        var credentialReference = configuration.CredentialRef;
        if (CredentialReferencePattern().Match(credentialReference.Trim()) is not { Success: true } match)
        {
            return false;
        }

        var name = match.Groups["name"].Value;
        var configuredCredential = appConfiguration[$"{ExternalAiOptions.SectionName}:Credentials:{name}"];
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
