using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace VeriScan.Api.Authentication;

public sealed class AdminRoleRequirement : IAuthorizationRequirement
{
}

public sealed class AdminPermissionRequirement(params string[] roles) : IAuthorizationRequirement
{
    public IReadOnlyCollection<string> Roles { get; } = roles;
}

public sealed class AdminRoleAuthorizationHandler(IOptions<AdminJwtOptions> options)
    : AuthorizationHandler<AdminRoleRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        AdminRoleRequirement requirement)
    {
        var requiredRole = options.Value.RequiredRole;
        var allowedRoles = AdminPolicies.AccessRoles.Append(requiredRole).ToHashSet(StringComparer.Ordinal);
        var hasRole = context.User.FindAll("role")
            .Any(claim => allowedRoles.Contains(claim.Value));
        var hasRealmRole = context.User.FindAll("realm_access")
            .Any(claim => HasRealmRole(claim.Value, allowedRoles));

        if (hasRole || hasRealmRole)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }

    private static bool HasRealmRole(string value, HashSet<string> allowedRoles)
    {
        try
        {
            using var document = JsonDocument.Parse(value);
            return document.RootElement.TryGetProperty("roles", out var roles) &&
                   roles.EnumerateArray().Any(role =>
                       role.GetString() is { } roleName && allowedRoles.Contains(roleName));
        }
        catch (JsonException)
        {
            return false;
        }
    }
}

public sealed class AdminPermissionAuthorizationHandler(IOptions<AdminJwtOptions> options)
    : AuthorizationHandler<AdminPermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        AdminPermissionRequirement requirement)
    {
        var roles = context.User.FindAll("role")
            .Select(claim => claim.Value)
            .Concat(context.User.FindAll("realm_access").SelectMany(claim => ReadRealmRoles(claim.Value)))
            .ToHashSet(StringComparer.Ordinal);
        if (roles.Contains(options.Value.RequiredRole) || requirement.Roles.Any(roles.Contains))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }

    private static string[] ReadRealmRoles(string value)
    {
        try
        {
            using var document = JsonDocument.Parse(value);
            return document.RootElement.TryGetProperty("roles", out var roles)
                ? roles.EnumerateArray()
                    .Select(role => role.GetString())
                    .Where(role => !string.IsNullOrWhiteSpace(role))
                    .Select(role => role!)
                    .ToArray()
                : [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}

public static class AdminPolicies
{
    public const string Viewer = "admin-viewer";
    public const string Operator = "admin-operator";
    public const string RuleEditor = "admin-rule-editor";
    public const string AiConfigEditor = "admin-ai-config-editor";
    public const string Publisher = "admin-publisher";
    public const string Auditor = "admin-auditor";
    public const string PlatformAdmin = "admin-platform";

    public const string ViewerRole = "veriscan-viewer";
    public const string OperatorRole = "veriscan-operator";
    public const string RuleEditorRole = "veriscan-ruleset-editor";
    public const string AiConfigEditorRole = "veriscan-ai-config-editor";
    public const string PublisherRole = "veriscan-publisher";
    public const string AuditorRole = "veriscan-auditor";
    public const string PlatformAdminRole = "veriscan-platform-admin";

    public static readonly IReadOnlyList<string> AccessRoles =
    [
        ViewerRole,
        OperatorRole,
        RuleEditorRole,
        AiConfigEditorRole,
        PublisherRole,
        AuditorRole,
        PlatformAdminRole
    ];
}
