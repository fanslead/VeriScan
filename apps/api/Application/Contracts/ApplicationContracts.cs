using System.ComponentModel.DataAnnotations;
using VeriScan.Domain.Entities;

namespace VeriScan.Application.Contracts;

/// <summary>创建应用请求。</summary>
public sealed record CreateApplicationRequest
{
    /// <summary>应用显示名称。</summary>
    [Required, StringLength(100, MinimumLength = 2)]
    public required string Name { get; init; }

    /// <summary>应用所在环境，只允许 test 或 live。</summary>
    [Required, RegularExpression("^(test|live)$")]
    public required string Environment { get; init; }
}

/// <summary>更新应用请求。</summary>
public sealed record UpdateApplicationRequest
{
    /// <summary>应用显示名称。</summary>
    [StringLength(100, MinimumLength = 2)]
    public string? Name { get; init; }

    /// <summary>应用状态。</summary>
    public ApplicationStatus? Status { get; init; }
}

/// <summary>应用返回模型。</summary>
public sealed record ApplicationResponse(
    Guid Id,
    string PublicId,
    string Name,
    string Environment,
    ApplicationStatus Status,
    int ActiveKeyCount,
    string? RuleSetRevisionId,
    string? RuleSetName,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <summary>应用列表返回模型。</summary>
public sealed record ApplicationListResponse(
    IReadOnlyList<ApplicationResponse> Items,
    int TotalCount);
