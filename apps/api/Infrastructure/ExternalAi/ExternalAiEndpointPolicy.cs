using System.Net;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using VeriScan.Application.Abstractions;

namespace VeriScan.Infrastructure.ExternalAi;

public sealed partial class ExternalAiEndpointPolicy(IOptionsMonitor<ExternalAiOptions> options) : IAiEndpointPolicy
{
    public void Validate(Uri endpoint)
    {
        if (!endpoint.IsAbsoluteUri || !string.Equals(endpoint.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new RequestValidationException("外部 AI 端点必须使用 HTTPS。");
        }

        if (!string.IsNullOrEmpty(endpoint.UserInfo) || !string.IsNullOrEmpty(endpoint.Query) || !string.IsNullOrEmpty(endpoint.Fragment))
        {
            throw new RequestValidationException("外部 AI 端点不得包含用户信息、查询参数或片段。");
        }

        if (endpoint.HostNameType is UriHostNameType.IPv4 or UriHostNameType.IPv6 || IPAddress.TryParse(endpoint.Host, out _))
        {
            throw new RequestValidationException("外部 AI 端点不得使用 IP 字面量。");
        }

        var host = NormalizeHost(endpoint.DnsSafeHost);
        if (host.Length == 0 || LocalHostPattern().IsMatch(host))
        {
            throw new RequestValidationException("外部 AI 端点不得指向本机地址。");
        }

        var configured = options.CurrentValue;
        if (configured.AllowedHosts.Length == 0 || !configured.AllowedHosts.Any(pattern => MatchesHost(pattern, host)))
        {
            throw new RequestValidationException("外部 AI 端点不在允许的主机列表中。");
        }

        var port = endpoint.IsDefaultPort ? 443 : endpoint.Port;
        if (port <= 0 || !configured.AllowedPorts.Contains(port))
        {
            throw new RequestValidationException("外部 AI 端口不在允许列表中。");
        }
    }

    private static bool MatchesHost(string pattern, string host)
    {
        var normalizedPattern = NormalizeHost(pattern);
        if (normalizedPattern.StartsWith("*.", StringComparison.Ordinal))
        {
            var suffix = normalizedPattern[1..];
            return host.EndsWith(suffix, StringComparison.Ordinal) && host.Length > suffix.Length;
        }

        return string.Equals(normalizedPattern, host, StringComparison.Ordinal);
    }

    private static string NormalizeHost(string host)
    {
        return host.Trim().TrimEnd('.').ToLowerInvariant();
    }

    [GeneratedRegex("(^|\\.)localhost$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex LocalHostPattern();
}
