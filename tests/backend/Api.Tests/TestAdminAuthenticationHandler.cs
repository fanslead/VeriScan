using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace VeriScan.Api.Tests;

internal sealed class TestAdminAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("Authorization", out var values) ||
            values.Count != 1 ||
            !values.ToString().StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var identity = new ClaimsIdentity(
            [new Claim("role", "veriscan-admin")],
            Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        return Task.FromResult(
            AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name)));
    }
}
