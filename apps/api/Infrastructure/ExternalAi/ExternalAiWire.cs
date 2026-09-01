using System.Text.Json;
using System.Text.RegularExpressions;
using VeriScan.Application.Abstractions;

namespace VeriScan.Infrastructure.ExternalAi;

internal sealed record ExternalAiCanonicalResult(
    AiModerationLabel Label,
    IReadOnlyList<string> ReasonCodes,
    IReadOnlyList<AiModerationCategory> Categories,
    IReadOnlyList<string> Evidence,
    bool EvidenceMismatch);

internal static partial class ExternalAiWire
{
    public static bool TryParseCanonical(
        string? json,
        string sourceContent,
        out ExternalAiCanonicalResult? result)
    {
        result = null;
        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !HasExactProperties(
                    root,
                    "label",
                    "categories",
                    "reasonCodes",
                    "evidence"))
            {
                return false;
            }

            if (!root.TryGetProperty("label", out var labelElement) ||
                labelElement.ValueKind != JsonValueKind.String ||
                !TryParseLabel(labelElement.GetString(), out var label))
            {
                return false;
            }

            if (!TryReadReasonCodes(root, out var reasonCodes) ||
                !TryReadCategories(root, out var categories) ||
                !TryReadEvidence(root, sourceContent, out var evidence, out var evidenceMismatch))
            {
                return false;
            }

            if (label == AiModerationLabel.Unsafe &&
                (reasonCodes.Count == 0 || categories.Count == 0 || evidence.Count == 0))
            {
                return false;
            }

            result = new ExternalAiCanonicalResult(label, reasonCodes, categories, evidence, evidenceMismatch);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool TryReadReasonCodes(
        JsonElement root,
        out IReadOnlyList<string> values)
    {
        if (!TryReadStringArray(root, "reasonCodes", 16, 64, out values))
        {
            return false;
        }

        return values.All(value => ReasonCodePattern().IsMatch(value));
    }

    private static bool TryReadStringArray(
        JsonElement root,
        string propertyName,
        int maximumItems,
        int maximumLength,
        out IReadOnlyList<string> values)
    {
        values = [];
        if (!root.TryGetProperty(propertyName, out var array) ||
            array.ValueKind != JsonValueKind.Array ||
            array.GetArrayLength() > maximumItems)
        {
            return false;
        }

        var parsed = new List<string>(array.GetArrayLength());
        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            var value = item.GetString();
            if (string.IsNullOrWhiteSpace(value) || value.Length > maximumLength)
            {
                return false;
            }

            parsed.Add(value);
        }

        values = parsed;
        return true;
    }

    private static bool TryReadCategories(
        JsonElement root,
        out IReadOnlyList<AiModerationCategory> categories)
    {
        categories = [];
        if (!root.TryGetProperty("categories", out var array) ||
            array.ValueKind != JsonValueKind.Array ||
            array.GetArrayLength() > 16)
        {
            return false;
        }

        var parsed = new List<AiModerationCategory>(array.GetArrayLength());
        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object || !HasExactProperties(item, "code", "severity") ||
                !item.TryGetProperty("code", out var codeElement) ||
                codeElement.ValueKind != JsonValueKind.String ||
                !item.TryGetProperty("severity", out var severityElement) ||
                severityElement.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            var code = codeElement.GetString();
            if (string.IsNullOrWhiteSpace(code) || code.Length > 64 ||
                !CategoryCodePattern().IsMatch(code) ||
                !TryParseSeverity(severityElement.GetString(), out var severity))
            {
                return false;
            }

            parsed.Add(new AiModerationCategory(code, severity));
        }

        categories = parsed;
        return true;
    }

    private static bool TryReadEvidence(
        JsonElement root,
        string sourceContent,
        out IReadOnlyList<string> evidence,
        out bool mismatch)
    {
        evidence = [];
        mismatch = false;
        if (!root.TryGetProperty("evidence", out var array) ||
            array.ValueKind != JsonValueKind.Array ||
            array.GetArrayLength() > 8)
        {
            return false;
        }

        var validQuotes = new List<string>(array.GetArrayLength());
        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object || !HasExactProperties(item, "quote") ||
                !item.TryGetProperty("quote", out var quoteElement) ||
                quoteElement.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            var quote = quoteElement.GetString();
            if (string.IsNullOrWhiteSpace(quote) || quote.Length > 256)
            {
                return false;
            }

            if (sourceContent.Contains(quote, StringComparison.Ordinal))
            {
                validQuotes.Add(quote);
            }
            else
            {
                mismatch = true;
            }
        }

        evidence = validQuotes;
        return true;
    }

    private static bool HasExactProperties(JsonElement element, params string[] expectedNames)
    {
        var expected = expectedNames.ToHashSet(StringComparer.Ordinal);
        var actual = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in element.EnumerateObject())
        {
            if (!actual.Add(property.Name) || !expected.Contains(property.Name))
            {
                return false;
            }
        }

        return actual.SetEquals(expected);
    }

    private static bool TryParseLabel(string? value, out AiModerationLabel label)
    {
        var normalized = value?.ToLowerInvariant();
        label = normalized switch
        {
            "safe" => AiModerationLabel.Safe,
            "unsafe" => AiModerationLabel.Unsafe,
            "review" => AiModerationLabel.Review,
            _ => default
        };
        return normalized is "safe" or "unsafe" or "review";
    }

    private static bool TryParseSeverity(string? value, out AiCategorySeverity severity)
    {
        var normalized = value?.ToLowerInvariant();
        severity = normalized switch
        {
            "low" => AiCategorySeverity.Low,
            "medium" => AiCategorySeverity.Medium,
            "high" => AiCategorySeverity.High,
            _ => default
        };
        return normalized is "low" or "medium" or "high";
    }

    [GeneratedRegex("^[A-Z][A-Z0-9_]{0,63}$", RegexOptions.CultureInvariant)]
    private static partial Regex ReasonCodePattern();

    [GeneratedRegex("^[a-z][a-z0-9_]{0,63}$", RegexOptions.CultureInvariant)]
    private static partial Regex CategoryCodePattern();
}
