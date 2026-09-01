using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace VeriScan.Api.Authentication;

public sealed class AdminRoleRequirement : IAuthorizationRequirement
{
}

public sealed class AdminRoleAuthorizationHandler(IOptions<AdminJwtOptions> options)
    : AuthorizationHandler<AdminRoleRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        AdminRoleRequirement requirement)
    {
        var requiredRole = options.Value.RequiredRole;
        var hasRole = context.User.FindAll("role")
            .Any(claim => string.Equals(claim.Value, requiredRole, StringComparison.Ordinal));
        var hasRealmRole = context.User.FindAll("realm_access")
            .Any(claim => HasRealmRole(claim.Value, requiredRole));

        if (hasRole || hasRealmRole)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }

    private static bool HasRealmRole(string value, string requiredRole)
    {
        try
        {
            using var document = JsonDocument.Parse(value);
            return document.RootElement.TryGetProperty("roles", out var roles) &&
                   roles.EnumerateArray().Any(role =>
                       string.Equals(role.GetString(), requiredRole, StringComparison.Ordinal));
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
