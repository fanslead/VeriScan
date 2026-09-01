using System.ComponentModel.DataAnnotations;

namespace VeriScan.Application.Contracts;

/// <summary>创建应用 API Key 请求。</summary>
public sealed record CreateApiKeyRequest
{
    /// <summary>Key 在管理后台展示的名称。</summary>
    [Required, StringLength(100, MinimumLength = 2)]
    public required string DisplayName { get; init; }

    /// <summary>Key 过期时间，必须晚于当前时间。</summary>
    [Required]
    public required DateTimeOffset ExpiresAt { get; init; }

    /// <summary>授权范围。</summary>
    [Required, MinLength(1), MaxLength(4)]
    public IReadOnlyList<string> Scopes { get; init; } = ["moderation:submit", "moderation:read"];
}

/// <summary>轮换应用 API Key 请求。</summary>
public sealed record RotateApiKeyRequest
{
    /// <summary>新 Key 在管理后台展示的名称。</summary>
    [Required, StringLength(100, MinimumLength = 2)]
    public required string DisplayName { get; init; }

    /// <summary>新 Key 过期时间。</summary>
    [Required]
    public required DateTimeOffset ExpiresAt { get; init; }

    /// <summary>是否在新 Key 创建后立即撤销旧 Key。</summary>
    public bool RevokeOldKey { get; init; }

    /// <summary>新 Key 授权范围。</summary>
    [Required, MinLength(1), MaxLength(4)]
    public IReadOnlyList<string> Scopes { get; init; } = ["moderation:submit", "moderation:read"];
}

/// <summary>一次性展示的 API Key 返回模型。</summary>
public sealed record ApiKeyCreatedResponse(
    Guid KeyId,
    string DisplayName,
    string KeyPrefix,
    string ApiKey,
    IReadOnlyList<string> Scopes,
    DateTimeOffset ExpiresAt);

/// <summary>API Key 脱敏列表项。</summary>
public sealed record ApiKeySummaryResponse(
    Guid KeyId,
    string DisplayName,
    string KeyPrefix,
    string LastFour,
    IReadOnlyList<string> Scopes,
    string Environment,
    string Status,
    DateTimeOffset NotBefore,
    DateTimeOffset ExpiresAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset? RevokedAt,
    DateTimeOffset? LastUsedAt);

/// <summary>API Key 列表返回模型。</summary>
public sealed record ApiKeyListResponse(
    IReadOnlyList<ApiKeySummaryResponse> Items,
    int TotalCount);
