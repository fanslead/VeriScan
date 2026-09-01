using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using VeriScan.Application.Abstractions;

namespace VeriScan.Api.Authentication;

public sealed class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions
{
}

public sealed class ApiKeyAuthenticationHandler(
    IOptionsMonitor<ApiKeyAuthenticationOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IApiKeyVerifier apiKeyVerifier)
    : AuthenticationHandler<ApiKeyAuthenticationOptions>(options, logger, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("X-API-Key", out var values) || values.Count != 1)
        {
            return AuthenticateResult.NoResult();
        }

        var presentedKey = values[0];
        if (string.IsNullOrWhiteSpace(presentedKey))
        {
            return AuthenticateResult.NoResult();
        }

        var key = await apiKeyVerifier.VerifyAsync(presentedKey, Context.RequestAborted);
        if (key is null)
        {
            return AuthenticateResult.Fail("invalid_api_key");
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, key.KeyId.ToString()),
            new("tenant_id", key.TenantId.ToString()),
            new("application_id", key.ApplicationId.ToString()),
            new("environment", key.Environment)
        };
        claims.AddRange(key.Scopes.Select(scope => new Claim("scope", scope)));
        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        return AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name));
    }

    protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        Response.ContentType = "application/problem+json";
        await Response.WriteAsJsonAsync(
            new ProblemDetails
            {
                Type = "https://veriscan.invalid/problems/invalid-api-key",
                Title = "Authentication required",
                Status = StatusCodes.Status401Unauthorized,
                Detail = "请求未通过认证。",
                Instance = Request.Path
            },
            options: null,
            contentType: "application/problem+json",
            cancellationToken: Context.RequestAborted);
    }

    protected override async Task HandleForbiddenAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        Response.ContentType = "application/problem+json";
        await Response.WriteAsJsonAsync(
            new ProblemDetails
            {
                Type = "https://veriscan.invalid/problems/forbidden",
                Title = "Forbidden",
                Status = StatusCodes.Status403Forbidden,
                Detail = "当前凭证无权执行该操作。",
                Instance = Request.Path
            },
            options: null,
            contentType: "application/problem+json",
            cancellationToken: Context.RequestAborted);
    }
}
