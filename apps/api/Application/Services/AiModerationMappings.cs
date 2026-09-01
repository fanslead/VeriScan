using VeriScan.Application.Abstractions;
using VeriScan.Domain.Entities;

namespace VeriScan.Application.Services;

internal static class AiModerationMappings
{
    public static RuleEvaluation ToEvaluation(AiModerationResult result, RuleEvaluation ruleFallback)
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
            result.Evidence);
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
