namespace VeriScan.Api.Authentication;

public sealed class AdminJwtOptions
{
    public const string SectionName = "Authentication:Admin";

    public const string Scheme = "AdminBearer";

    public const string Policy = "admin-management";

    public string Authority { get; set; } = string.Empty;

    public string? MetadataAddress { get; set; }

    public string Audience { get; set; } = "veriscan-api";

    public bool RequireHttpsMetadata { get; set; } = true;

    public bool ValidateAudience { get; set; } = true;

    public string RequiredRole { get; set; } = "veriscan-admin";
}
