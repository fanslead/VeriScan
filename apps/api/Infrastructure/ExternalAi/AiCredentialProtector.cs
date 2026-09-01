using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using VeriScan.Application.Abstractions;

namespace VeriScan.Infrastructure.ExternalAi;

public sealed class AiCredentialProtector(IOptions<AiCredentialEncryptionOptions> options)
    : IAiCredentialProtector
{
    private const string Version = "v1";
    private static readonly byte[] AssociatedData = Encoding.UTF8.GetBytes("veriscan-ai-credential:v1");
    private readonly byte[] key = Convert.FromBase64String(options.Value.MasterKey);

    public string Protect(string credential)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(credential);
        var plaintext = Encoding.UTF8.GetBytes(credential);
        var nonce = RandomNumberGenerator.GetBytes(12);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[16];
        using var aes = new AesGcm(key, tag.Length);
        aes.Encrypt(nonce, plaintext, ciphertext, tag, AssociatedData);
        CryptographicOperations.ZeroMemory(plaintext);
        return string.Join(
            '.',
            Version,
            Convert.ToBase64String(nonce),
            Convert.ToBase64String(tag),
            Convert.ToBase64String(ciphertext));
    }

    public bool TryUnprotect(string protectedCredential, out string credential)
    {
        credential = string.Empty;
        try
        {
            var parts = protectedCredential.Split('.', StringSplitOptions.None);
            if (parts.Length != 4 || parts[0] != Version)
            {
                return false;
            }

            var nonce = Convert.FromBase64String(parts[1]);
            var tag = Convert.FromBase64String(parts[2]);
            var ciphertext = Convert.FromBase64String(parts[3]);
            if (nonce.Length != 12 || tag.Length != 16 || ciphertext.Length == 0)
            {
                return false;
            }

            var plaintext = new byte[ciphertext.Length];
            using var aes = new AesGcm(key, tag.Length);
            aes.Decrypt(nonce, ciphertext, tag, plaintext, AssociatedData);
            credential = Encoding.UTF8.GetString(plaintext);
            CryptographicOperations.ZeroMemory(plaintext);
            return !string.IsNullOrWhiteSpace(credential);
        }
        catch (Exception exception) when (
            exception is FormatException or CryptographicException or ArgumentException)
        {
            return false;
        }
    }
}
