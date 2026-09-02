using System.ComponentModel.DataAnnotations;

namespace VeriScan.Infrastructure.Webhooks;

/// <summary>Webhook 供应商连接和密钥轮换配置。</summary>
public sealed class WebhookProviderOptions : IValidatableObject
{
    public const string SectionName = "WebhookProvider";

    /// <summary>是否启用供应商适配器。</summary>
    public bool Enabled { get; set; }

    /// <summary>Svix Server URL；不包含末尾斜杠。</summary>
    public string ServerUrl { get; set; } = string.Empty;

    /// <summary>Svix 管理 API 认证令牌。</summary>
    public string AuthToken { get; set; } = string.Empty;

    /// <summary>轮换签名密钥时旧密钥继续有效的秒数。</summary>
    [Range(0, 7 * 24 * 60 * 60)]
    public int SecretRotationGraceSeconds { get; set; } = 24 * 60 * 60;

    /// <summary>每次供应商 API 调用的超时时间。</summary>
    [Range(100, 120_000)]
    public int TimeoutMilliseconds { get; set; } = 15_000;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Enabled && string.IsNullOrWhiteSpace(AuthToken))
        {
            yield return new ValidationResult(
                "WebhookProvider:AuthToken 在启用 Webhook 供应商时不能为空。",
                [nameof(AuthToken)]);
        }

        if (Enabled && !TryCreateServerUri(ServerUrl, out _))
        {
            yield return new ValidationResult(
                "WebhookProvider:ServerUrl 必须是绝对 HTTP 或 HTTPS 地址。",
                [nameof(ServerUrl)]);
        }
        else if (!string.IsNullOrWhiteSpace(ServerUrl) && !TryCreateServerUri(ServerUrl, out _))
        {
            yield return new ValidationResult(
                "WebhookProvider:ServerUrl 必须是绝对 HTTP 或 HTTPS 地址。",
                [nameof(ServerUrl)]);
        }
    }

    internal static bool TryCreateServerUri(string value, out Uri? uri)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out uri))
        {
            return false;
        }

        return uri.Scheme is "http" or "https" &&
               string.IsNullOrEmpty(uri.UserInfo) &&
               string.IsNullOrEmpty(uri.Fragment) &&
               string.IsNullOrEmpty(uri.Query);
    }
}
