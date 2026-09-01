using System.Text.Json;
using VeriScan.Application.Abstractions;
using VeriScan.Application.Contracts;
using VeriScan.Domain.Entities;

namespace VeriScan.Application.Services;

internal static class AdminReadMappings
{
    public static AdminOverviewResponse ToOverviewResponse(
        AdminOverviewReadData data,
        AdminOverviewReadData previous,
        IModerationContentProtector contentProtector)
    {
        var total = data.TodayItems;
        var rejectRate = CalculateRate(data.RejectCount, total);
        var reviewRate = CalculateRate(data.ReviewCount, total);
        var previousRejectRate = CalculateRate(previous.RejectCount, previous.TodayItems);
        var previousReviewRate = CalculateRate(previous.ReviewCount, previous.TodayItems);
        return new AdminOverviewResponse(
            data.TodayRequests,
            data.TodayItems,
            data.PassCount,
            data.RejectCount,
            data.ReviewCount,
            rejectRate,
            reviewRate,
            data.P95LatencyMs,
            data.Trend.Select(ToTrendPoint).ToArray(),
            data.RecentRecords.Select(item =>
                ToResponse(item, contentProtector.Unprotect(item.Content))).ToArray(),
            data.DataThrough,
            CalculatePercentDelta(data.TodayRequests, previous.TodayRequests),
            CalculatePointDelta(rejectRate, previousRejectRate),
            CalculatePointDelta(reviewRate, previousReviewRate),
            CalculatePercentDelta(data.P95LatencyMs, previous.P95LatencyMs));
    }

    public static ModerationRecordPageResponse ToPageResponse(
        AdminModerationRecordPageReadData data,
        int page,
        int pageSize,
        IModerationContentProtector contentProtector)
    {
        return new ModerationRecordPageResponse(
            data.Items.Select(item =>
                ToResponse(item, contentProtector.Unprotect(item.Content))).ToArray(),
            data.Total,
            page,
            pageSize);
    }

    public static ModerationRecordResponse ToResponse(
        AdminModerationRecordReadData data,
        string plaintextContent)
    {
        var reasonCodes = DeserializeReasonCodes(data.ReasonCodesJson);
        var categories = DeserializeCategories(data.CategoriesJson);
        var evidence = DeserializeEvidence(data.EvidenceJson);
        var decision = ParseDecision(data.Decision);
        int? latency = data.MachineCompletedAt.HasValue
            ? Math.Max(0, (int)Math.Round(
                (data.MachineCompletedAt.Value - data.CreatedAt).TotalMilliseconds,
                MidpointRounding.AwayFromZero))
            : null;

        return new ModerationRecordResponse(
            data.Id,
            data.RequestId,
            data.ApplicationId,
            data.ApplicationName,
            CreatePreview(plaintextContent),
            data.ContentHash,
            decision,
            data.RiskScore,
            data.ScoreSource,
            categories.FirstOrDefault()?.Code,
            data.ReviewSource,
            ToDetectLevel(data.Route),
            latency,
            data.CreatedAt,
            reasonCodes,
            evidence,
            data.PolicyVersion,
            data.ErrorCode,
            data.Route,
            categories,
            data.AiConfigurationRevision,
            data.ProviderRequestId,
            data.AiInputTokens,
            data.AiOutputTokens,
            data.AiFailureCode);
    }

    private static decimal? CalculateRate(long count, long total)
    {
        return total == 0 ? null : decimal.Round(count * 100m / total, 2);
    }

    private static decimal? CalculatePercentDelta(decimal? current, decimal? previous)
    {
        return previous is null or 0 || current is null
            ? null
            : decimal.Round((current.Value - previous.Value) * 100m / previous.Value, 2);
    }

    private static decimal? CalculatePercentDelta(long current, long previous)
    {
        return previous == 0
            ? null
            : decimal.Round((current - previous) * 100m / previous, 2);
    }

    private static decimal? CalculatePointDelta(decimal? current, decimal? previous)
    {
        return current is null || previous is null
            ? null
            : decimal.Round(current.Value - previous.Value, 2);
    }

    private static ModerationTrendPoint ToTrendPoint(AdminOverviewTrendReadData data)
    {
        return new ModerationTrendPoint($"{data.Hour:00}:00", data.Total, data.Reject, data.Review);
    }

    private static ModerationDecision? ParseDecision(string? value)
    {
        return Enum.TryParse<ModerationDecision>(value, ignoreCase: true, out var decision)
            ? decision
            : null;
    }

    private static int? ToDetectLevel(string route)
    {
        return route switch
        {
            "local_rules" or "rules" => 1,
            var value when value.StartsWith("external_ai", StringComparison.Ordinal) => 2,
            _ => null
        };
    }

    private static string[] DeserializeReasonCodes(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<string[]>(value) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static string[] DeserializeEvidence(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        try
        {
            var structured = JsonSerializer.Deserialize<RuleEvidence[]>(value);
            if (structured is not null)
            {
                return structured
                    .Select(item => item.Quote)
                    .Where(quote => !string.IsNullOrWhiteSpace(quote))
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();
            }
        }
        catch (JsonException)
        {
            // 兼容历史记录中的字符串证据数组。
        }

        try
        {
            return JsonSerializer.Deserialize<string[]>(value) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static ModerationCategoryResponse[] DeserializeCategories(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        try
        {
            var categories = JsonSerializer.Deserialize<RuleCategory[]>(value) ?? [];
            return categories
                .Select(category => new ModerationCategoryResponse(category.Code, category.RiskScore))
                .ToArray();
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static string CreatePreview(string content)
    {
        const int maximumLength = 240;
        return content.Length <= maximumLength ? content : content[..maximumLength];
    }
}
