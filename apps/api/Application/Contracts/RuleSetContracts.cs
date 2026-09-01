using System.ComponentModel.DataAnnotations;
using VeriScan.Domain.Entities;

namespace VeriScan.Application.Contracts;

public sealed record CreateRuleSetRequest : RuleSetDraftRequest;

public record RuleSetDraftRequest
{
    [Required, StringLength(100, MinimumLength = 2)]
    public required string Name { get; init; }

    [Required, MinLength(1), MaxLength(5000)]
    public IReadOnlyList<WordRuleDraftRequest> Rules { get; init; } = [];
}

public sealed record WordRuleDraftRequest
{
    [Required, StringLength(200, MinimumLength = 1)]
    public required string Term { get; init; }

    public required WordRuleType Type { get; init; }

    [Required, StringLength(64, MinimumLength = 1)]
    public required string Category { get; init; }

    [Range(0, 1)]
    public decimal Weight { get; init; }
}

public sealed record RuleSetResponse(
    Guid Id,
    string PublicRevisionId,
    string Name,
    RuleSetStatus Status,
    int RuleCount,
    bool RulesTruncated,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? LastValidatedAt,
    string? LastValidatedChecksum,
    DateTimeOffset? PublishedAt,
    string? PublishedChecksum,
    int ApplicationCount,
    IReadOnlyList<WordRuleResponse> Rules);

public sealed record WordRuleResponse(
    Guid Id,
    string Term,
    WordRuleType Type,
    string Category,
    decimal Weight,
    bool IsEnabled);

public sealed record RuleSetListResponse(IReadOnlyList<RuleSetResponse> Items);

public sealed record RuleSetValidationResponse(
    bool Valid,
    string Checksum,
    int RuleCount,
    IReadOnlyList<RuleSetValidationIssue> Issues);

public sealed record RuleSetValidationIssue(
    string Code,
    string Message,
    int? RuleIndex);

public sealed record BindApplicationRuleSetRequest
{
    [Required, StringLength(80, MinimumLength = 10)]
    public required string PublicRevisionId { get; init; }
}
