using System.Security.Claims;
using VeriScan.Application.Abstractions;

namespace VeriScan.Api.Authentication;

public static class ApiKeyPrincipalExtensions
{
    public static ApiKeyPrincipalData GetApiKeyPrincipal(this ClaimsPrincipal principal)
    {
        if (!Guid.TryParse(principal.FindFirstValue("tenant_id"), out var tenantId) ||
            !Guid.TryParse(principal.FindFirstValue("application_id"), out var applicationId) ||
            !Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var keyId))
        {
            throw new InvalidOperationException("认证主体信息不完整。");
        }

        return new ApiKeyPrincipalData(
            tenantId,
            applicationId,
            keyId,
            principal.FindFirstValue("environment") ?? string.Empty,
            principal.FindAll("scope").Select(claim => claim.Value).ToArray());
    }
}
