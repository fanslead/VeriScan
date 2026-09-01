using System.Text.Json;
using VeriScan.Application.Contracts;
using VeriScan.Domain.Entities;

namespace VeriScan.Application.Services;

internal static class ApplicationMappings
{
    public static ApplicationResponse ToResponse(ApplicationEntity application, int activeKeyCount)
    {
        return new ApplicationResponse(
            application.Id,
            application.PublicId,
            application.Name,
            application.EnvironmentName,
            application.Status,
            activeKeyCount,
            application.RuleSetVersion?.PublicRevisionId,
            application.RuleSetVersion?.Name,
            application.CreatedAt,
            application.UpdatedAt);
    }

    public static ApplicationResponse ToResponseWithRuleSet(
        ApplicationEntity application,
        RuleSetVersion? ruleSet,
        int activeKeyCount)
    {
        return new ApplicationResponse(
            application.Id,
            application.PublicId,
            application.Name,
            application.EnvironmentName,
            application.Status,
            activeKeyCount,
            ruleSet?.PublicRevisionId,
            ruleSet?.Name,
            application.CreatedAt,
            application.UpdatedAt);
    }
}

internal static class ApiKeyMappings
{
    public static ApiKeySummaryResponse ToSummary(ApplicationApiKey key)
    {
        return new ApiKeySummaryResponse(
            key.Id,
            key.DisplayName,
            key.KeyPrefix,
            key.LastFour,
            key.ScopesText.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            key.EnvironmentName,
            key.Status.ToString().ToLowerInvariant(),
            key.NotBefore,
            key.ExpiresAt,
            key.CreatedAt,
            key.RevokedAt,
            key.LastUsedAt);
    }
}

internal static class ModerationMappings
{
    public static BatchModerationResponse ToResponse(ModerationRequest request)
    {
        return new BatchModerationResponse(
            request.Id,
            request.ApplicationId,
            request.PolicyRevision,
            ToWireStatus(request.ProcessingStatus),
            request.SubmittedAt,
            request.MachineCompletedAt,
            request.FinalizedAt,
            request.Items.OrderBy(item => item.Ordinal).Select(ToResponse).ToArray());
    }

    private static ModerationItemResponse ToResponse(ModerationItem item)
    {
        var reasonCodes = DeserializeList(item.ReasonCodesText);
        var categories = DeserializeCategories(item.CategoriesText);
        var evidence = DeserializeEvidence(item.EvidenceText);
        return new ModerationItemResponse(
            item.ClientItemId,
            ToWireStatus(item.ProcessingStatus),
            item.Decision,
            item.Decision == ModerationDecision.Review,
            item.ReviewSource,
            item.Degraded,
            item.RiskScore,
            item.ScoreSource,
            reasonCodes,
            categories,
            item.Route,
            item.ErrorCode,
            item.MachineCompletedAt,
            item.FinalizedAt,
            evidence);
    }

    private static string[] DeserializeList(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? []
            : JsonSerializer.Deserialize<string[]>(value) ?? [];
    }

    private static ModerationCategoryResponse[] DeserializeCategories(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        var categories = JsonSerializer.Deserialize<RuleCategory[]>(value) ?? [];
        return categories.Select(category => new ModerationCategoryResponse(category.Code, category.RiskScore)).ToArray();
    }

    private static ModerationEvidenceResponse[] DeserializeEvidence(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        try
        {
            var evidence = JsonSerializer.Deserialize<RuleEvidence[]>(value) ?? [];
            return evidence
                .Select(item => new ModerationEvidenceResponse(
                    item.RuleId,
                    item.RuleKind,
                    item.Category,
                    item.Action,
                    item.Quote,
                item.OriginalStart,
                item.OriginalLength,
                item.NormalizedStart,
                item.NormalizedLength,
                item.EvidenceTemplate))
                .ToArray();
        }
        catch (JsonException)
        {
            try
            {
                var legacyEvidence = JsonSerializer.Deserialize<string[]>(value) ?? [];
                return legacyEvidence
                    .Select(quote => new ModerationEvidenceResponse(
                        string.Empty,
                        "ai",
                        string.Empty,
                        RuleAction.MonitorOnly,
                        quote,
                        -1,
                        -1,
                        -1,
                        -1))
                    .ToArray();
            }
            catch (JsonException)
            {
                return [];
            }
        }
    }

    private static string ToWireStatus(ModerationProcessingStatus status)
    {
        return status switch
        {
            ModerationProcessingStatus.Accepted => "accepted",
            ModerationProcessingStatus.Processing => "processing",
            ModerationProcessingStatus.RetryWait => "retry_wait",
            ModerationProcessingStatus.Completed => "completed",
            ModerationProcessingStatus.CompletedWithErrors => "completed_with_errors",
            ModerationProcessingStatus.Failed => "failed",
            ModerationProcessingStatus.Cancelled => "cancelled",
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
        };
    }
}
