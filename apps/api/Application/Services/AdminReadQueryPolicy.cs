using VeriScan.Application.Abstractions;
using VeriScan.Application.Contracts;
using VeriScan.Domain.Entities;

namespace VeriScan.Application.Services;

internal sealed record NormalizedAdminModerationRecordQuery(
    Guid? ApplicationId,
    string? Decision,
    string? Keyword,
    int Page,
    int PageSize);

internal static class AdminReadQueryPolicy
{
    public static NormalizedAdminModerationRecordQuery Normalize(
        AdminModerationRecordQuery query)
    {
        var decision = NormalizeDecision(query.Decision, query.Status);
        var page = query.Page ?? 1;
        var pageSize = query.PageSize ?? 20;
        ValidatePaging(page, pageSize);

        var keyword = query.Keyword?.Trim();
        if (keyword is not null && keyword.Length == 0)
        {
            keyword = null;
        }

        if (keyword is not null && keyword.Length > 128)
        {
            throw new RequestValidationException("keyword 长度不能超过 128 个字符。");
        }

        return new NormalizedAdminModerationRecordQuery(
            query.ApplicationId,
            decision,
            keyword,
            page,
            pageSize);
    }

    private static string? NormalizeDecision(string? decision, string? status)
    {
        var normalizedDecision = NormalizeFilterValue(decision);
        var normalizedStatus = NormalizeFilterValue(status);
        if (normalizedDecision is not null && normalizedStatus is not null &&
            !string.Equals(normalizedDecision, normalizedStatus, StringComparison.Ordinal))
        {
            throw new RequestValidationException("decision 与 status 不能同时指定不同的值。");
        }

        var value = normalizedDecision ?? normalizedStatus;
        if (value is null || value == "all")
        {
            return null;
        }

        if (!Enum.TryParse<ModerationDecision>(value, ignoreCase: true, out var parsed) ||
            !Enum.IsDefined(parsed))
        {
            throw new RequestValidationException("审核决定只能是 pass、reject 或 review。");
        }

        return parsed.ToString();
    }

    private static string? NormalizeFilterValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();
    }

    private static void ValidatePaging(int page, int pageSize)
    {
        if (page < 1 || page > AdminModerationRecordQuery.MaximumPage)
        {
            throw new RequestValidationException(
                $"page 必须在 1 到 {AdminModerationRecordQuery.MaximumPage} 之间。");
        }

        if (pageSize < 1 || pageSize > AdminModerationRecordQuery.MaximumPageSize)
        {
            throw new RequestValidationException(
                $"pageSize 必须在 1 到 {AdminModerationRecordQuery.MaximumPageSize} 之间。");
        }
    }
}
