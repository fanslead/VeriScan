using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using VeriScan.Api.Authentication;

namespace VeriScan.Api.Tests;

public sealed class AdminAuthorizationTests
{
    [Fact]
    public async Task KeycloakRealmAccessRoleIsAccepted()
    {
        var identity = new ClaimsIdentity(
            [new Claim("realm_access", "{\"roles\":[\"veriscan-admin\"]}")],
            "AdminBearer");
        var context = new AuthorizationHandlerContext(
            [new AdminRoleRequirement()],
            new ClaimsPrincipal(identity),
            null);
        var handler = new AdminRoleAuthorizationHandler(Options.Create(new AdminJwtOptions()));

        await handler.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task GranularWorkspaceRoleCanEnterAdminApi()
    {
        var identity = new ClaimsIdentity(
            [new Claim("realm_access", "{\"roles\":[\"veriscan-viewer\"]}")],
            "AdminBearer");
        var context = new AuthorizationHandlerContext(
            [new AdminRoleRequirement()],
            new ClaimsPrincipal(identity),
            null);
        var handler = new AdminRoleAuthorizationHandler(Options.Create(new AdminJwtOptions()));

        await handler.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task UnknownRealmRoleCannotEnterAdminApi()
    {
        var identity = new ClaimsIdentity(
            [new Claim("realm_access", "{\"roles\":[\"unrelated-role\"]}")],
            "AdminBearer");
        var context = new AuthorizationHandlerContext(
            [new AdminRoleRequirement()],
            new ClaimsPrincipal(identity),
            null);
        var handler = new AdminRoleAuthorizationHandler(Options.Create(new AdminJwtOptions()));

        await handler.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }
}
