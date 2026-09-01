using System.Net;
using VeriScan.Application.Abstractions;
using VeriScan.Domain.Entities;

namespace VeriScan.Infrastructure.ExternalAi;

internal sealed record ExternalAiProviderParse(
    AiModerationOutcome Outcome,
    ExternalAiCanonicalResult? Canonical,
    string? ProviderRequestId,
    int? InputTokens,
    int? OutputTokens,
    string? FailureCode);

internal static class ExternalAiResultMapping
{
    public static AiModerationResult FromHttp(
        AiModelConfiguration configuration,
        ExternalAiHttpResult response,
        Func<string, ExternalAiProviderParse> parse)
    {
        if (response.FailureCode is not null)
        {
            return Failure(
                configuration,
                AiModerationOutcome.Unavailable,
                response.FailureCode,
                response.ProviderRequestId);
        }

        if (response.StatusCode is not { } statusCode || (int)statusCode is < 200 or >= 300)
        {
            return Failure(
                configuration,
                AiModerationOutcome.Unavailable,
                MapHttpFailure(response.StatusCode),
                response.ProviderRequestId);
        }

        if (response.Body is null)
        {
            return Failure(
                configuration,
                AiModerationOutcome.InvalidOutput,
                "AI_RESPONSE_TOO_LARGE",
                response.ProviderRequestId);
        }

        var parsed = parse(response.Body);
        var providerRequestId = parsed.ProviderRequestId ?? response.ProviderRequestId;
        if (parsed.Outcome != AiModerationOutcome.Succeeded || parsed.Canonical is null)
        {
            return Failure(
                configuration,
                parsed.Outcome,
                parsed.FailureCode ?? MapOutcomeFailure(parsed.Outcome),
                providerRequestId,
                parsed.InputTokens,
                parsed.OutputTokens);
        }

        var reasonCodes = parsed.Canonical.ReasonCodes;
        if (parsed.Canonical.EvidenceMismatch && !reasonCodes.Contains("AI_EVIDENCE_MISMATCH", StringComparer.Ordinal))
        {
            reasonCodes = reasonCodes
                .Append("AI_EVIDENCE_MISMATCH")
                .Take(16)
                .ToArray();
        }

        return new AiModerationResult(
            AiModerationOutcome.Succeeded,
            parsed.Canonical.Label,
            reasonCodes,
            parsed.Canonical.Categories,
            parsed.Canonical.Evidence,
            configuration.PublicRevisionId,
            providerRequestId,
            parsed.InputTokens,
            parsed.OutputTokens,
            null);
    }

    public static AiModerationResult Failure(
        AiModelConfiguration configuration,
        AiModerationOutcome outcome,
        string failureCode,
        string? providerRequestId = null,
        int? inputTokens = null,
        int? outputTokens = null)
    {
        return new AiModerationResult(
            outcome,
            null,
            [],
            [],
            [],
            configuration.PublicRevisionId,
            providerRequestId,
            inputTokens,
            outputTokens,
            failureCode);
    }

    private static string MapHttpFailure(HttpStatusCode? statusCode)
    {
        return statusCode switch
        {
            HttpStatusCode.TooManyRequests => "AI_RATE_LIMITED",
            >= HttpStatusCode.InternalServerError => "AI_PROVIDER_5XX",
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => "AI_PROVIDER_AUTH_FAILED",
            >= HttpStatusCode.MultipleChoices and < HttpStatusCode.BadRequest => "AI_REDIRECT_BLOCKED",
            _ => "AI_PROVIDER_4XX"
        };
    }

    private static string MapOutcomeFailure(AiModerationOutcome outcome)
    {
        return outcome switch
        {
            AiModerationOutcome.ProviderRefusal => "AI_PROVIDER_REFUSAL",
            AiModerationOutcome.Truncated => "AI_OUTPUT_TRUNCATED",
            AiModerationOutcome.InvalidOutput => "AI_OUTPUT_INVALID",
            _ => "AI_UNAVAILABLE"
        };
    }
}
