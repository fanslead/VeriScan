using System.ComponentModel.DataAnnotations;

namespace VeriScan.Infrastructure.ExternalAi;

public sealed class AiCredentialEncryptionOptions
{
    public const string SectionName = "Security:AiCredentials";

    [Required]
    public string MasterKey { get; set; } = string.Empty;

    public static bool HasValidMasterKey(string value)
    {
        try
        {
            return Convert.FromBase64String(value).Length == 32;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
