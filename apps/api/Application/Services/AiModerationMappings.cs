using VeriScan.Application.Abstractions;
using VeriScan.Domain.Entities;

namespace VeriScan.Application.Services;

internal static class AiModerationMappings
{
    public static RuleEvaluation ToEvaluation(
        AiModerationResult result,
        RuleEvaluation ruleFallback,
        string content,
        RuleNormalizationOptions normalizationOptions)
    {
        if (result.Outcome != AiModerationOutcome.Succeeded || result.Label is null)
        {
            return ToDegradedReview(result, ruleFallback);
        }

        var decision = result.Label switch
        {
            AiModerationLabel.Safe => ModerationDecision.Pass,
            AiModerationLabel.Unsafe => ModerationDecision.Reject,
            _ => ModerationDecision.Review
        };
        var categories = result.Categories
            .Select(category => new RuleCategory(category.Code, null))
            .ToArray();
        var aiEvidence = result.Evidence
            .Select(quote => TryLocateEvidence(quote, content, normalizationOptions))
            .Where(evidence => evidence is not null)
            .Cast<RuleEvidence>()
            .ToArray();
        return new RuleEvaluation(
            decision,
            false,
            decision == ModerationDecision.Review ? "ai_model" : null,
            false,
            null,
            null,
            $"external_ai:{result.ConfigurationRevision}",
            result.ReasonCodes,
            categories,
            result.Evidence)
        {
            EvidenceDetails = ruleFallback.EvidenceDetails.Concat(aiEvidence).ToArray()
        };
    }

    private static RuleEvidence? TryLocateEvidence(
        string quote,
        string content,
        RuleNormalizationOptions normalizationOptions)
    {
        if (string.IsNullOrWhiteSpace(quote))
        {
            return null;
        }

        var normalizedContent = RuleTextNormalizer.Normalize(content, normalizationOptions);
        var normalizedQuote = RuleTextNormalizer.NormalizeValue(quote, normalizationOptions);
        if (normalizedQuote.Length == 0)
        {
            return null;
        }

        var normalizedStart = normalizedContent.Value.IndexOf(
            normalizedQuote,
            StringComparison.Ordinal);
        if (normalizedStart < 0 ||
            normalizedStart + normalizedQuote.Length > normalizedContent.Spans.Count)
        {
            return null;
        }

        var first = normalizedContent.Spans[normalizedStart];
        var last = normalizedContent.Spans[normalizedStart + normalizedQuote.Length - 1];
        var originalStart = first.OriginalStart;
        var originalEnd = last.OriginalStart + last.OriginalLength;
        if (originalStart < 0 || originalEnd > content.Length || originalEnd <= originalStart)
        {
            return null;
        }

        return new RuleEvidence(
            string.Empty,
            "ai",
            string.Empty,
            RuleAction.MonitorOnly,
            content.Substring(originalStart, originalEnd - originalStart),
            originalStart,
            originalEnd - originalStart,
            normalizedStart,
            normalizedQuote.Length);
    }

    private static RuleEvaluation ToDegradedReview(
        AiModerationResult result,
        RuleEvaluation ruleFallback)
    {
        var failureCode = result.Outcome switch
        {
            AiModerationOutcome.NoActiveConfiguration => "AI_ROUTE_NOT_CONFIGURED",
            AiModerationOutcome.ProviderRefusal => "AI_PROVIDER_REFUSAL",
            AiModerationOutcome.Truncated => "AI_OUTPUT_TRUNCATED",
            AiModerationOutcome.InvalidOutput => "AI_OUTPUT_INVALID",
            AiModerationOutcome.PolicyDenied => "AI_EXTERNAL_POLICY_DENIED",
            _ => "AI_UNAVAILABLE"
        };
        return ruleFallback with
        {
            RequiresAi = false,
            ReviewSource = result.Outcome switch
            {
                AiModerationOutcome.NoActiveConfiguration => "policy_required",
                AiModerationOutcome.ProviderRefusal => "provider_refusal",
                _ => "ai_failure"
            },
            Degraded = result.Outcome != AiModerationOutcome.NoActiveConfiguration,
            Route = result.ConfigurationRevision is null
                ? "local_rules"
                : $"external_ai:{result.ConfigurationRevision}",
            ReasonCodes = [failureCode, "CALLER_REVIEW_REQUIRED"],
            Evidence = []
        };
    }
}
