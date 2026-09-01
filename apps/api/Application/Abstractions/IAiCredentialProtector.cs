namespace VeriScan.Application.Abstractions;

/// <summary>
/// Protects provider credentials before persistence and restores them only for an outbound request.
/// </summary>
public interface IAiCredentialProtector
{
    string Protect(string credential);

    bool TryUnprotect(string protectedCredential, out string credential);
}
