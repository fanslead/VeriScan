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
            ToCamelCase(request.ProcessingStatus.ToString()),
            request.SubmittedAt,
            request.MachineCompletedAt,
            request.FinalizedAt,
            request.Items.Select(ToResponse).ToArray());
    }

    private static ModerationItemResponse ToResponse(ModerationItem item)
    {
        var reasonCodes = DeserializeList(item.ReasonCodesText);
        var categories = DeserializeCategories(item.CategoriesText);
        return new ModerationItemResponse(
            item.ClientItemId,
            ToCamelCase(item.ProcessingStatus.ToString()),
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
            item.FinalizedAt);
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

    private static string ToCamelCase(string value)
    {
        return value.Length == 0
            ? value
            : char.ToLowerInvariant(value[0]) + value[1..];
    }
}
