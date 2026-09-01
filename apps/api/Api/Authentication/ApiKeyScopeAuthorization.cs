using Microsoft.AspNetCore.Authorization;

namespace VeriScan.Api.Authentication;

public sealed class ScopeRequirement(string scope) : IAuthorizationRequirement
{
    public string Scope { get; } = scope;
}

public sealed class ScopeAuthorizationHandler : AuthorizationHandler<ScopeRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ScopeRequirement requirement)
    {
        if (context.User.Claims.Any(
                claim => claim.Type == "scope" &&
                         string.Equals(claim.Value, requirement.Scope, StringComparison.Ordinal)))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
