using Microsoft.Extensions.Options;
using VeriScan.Application.Abstractions;

namespace VeriScan.Infrastructure.Security;

public sealed class ApiKeyPolicy(IOptions<ApiKeyOptions> options) : IApiKeyPolicy
{
    private static readonly HashSet<string> AllowedScopes =
    [
        "moderation:submit",
        "moderation:read"
    ];

    private readonly ApiKeyOptions settings = options.Value;

    public int MaximumActiveKeys => settings.MaximumActiveKeys;

    public TimeSpan MaximumLifetime => TimeSpan.FromDays(settings.MaximumLifetimeDays);

    public bool IsAllowedScope(string scope)
    {
        return AllowedScopes.Contains(scope);
    }
}
