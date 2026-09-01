using System.Text.Json;
using VeriScan.Domain.Entities;

namespace VeriScan.Application.Services;

/// <summary>生成不包含原文、凭证和完整 API Key 的事实摘要。</summary>
internal static class OperationalFactPayloads
{
    public static string Application(ApplicationEntity application, string action)
    {
        return Serialize(new
        {
            applicationId = application.Id,
            publicId = application.PublicId,
            action,
            status = application.Status.ToString(),
            environment = application.EnvironmentName,
            ruleSetVersionId = application.RuleSetVersionId
        });
    }

    public static string ApiKey(ApplicationApiKey apiKey, string action)
    {
        return Serialize(new
        {
            apiKeyId = apiKey.Id,
            applicationId = apiKey.ApplicationId,
            publicKeyId = apiKey.PublicKeyId,
            keyPrefix = apiKey.KeyPrefix,
            action,
            status = apiKey.Status.ToString(),
            expiresAt = apiKey.ExpiresAt
        });
    }

    public static string AiConfiguration(AiModelConfiguration configuration, string action)
    {
        return Serialize(new
        {
            configurationId = configuration.Id,
            revision = configuration.PublicRevisionId,
            action,
            protocol = configuration.Protocol.ToString(),
            model = configuration.Model,
            status = configuration.Status.ToString(),
            configuration.IsActive
        });
    }

    public static string RuleSet(RuleSetVersion ruleSet, string action)
    {
        return Serialize(new
        {
            ruleSetVersionId = ruleSet.Id,
            revision = ruleSet.PublicRevisionId,
            action,
            status = ruleSet.Status.ToString(),
            ruleCount = ruleSet.Rules.Count + ruleSet.RegexRules.Count + ruleSet.CombinationRules.Count,
            normalizationProfile = ruleSet.NormalizationProfile.ToString(),
            checksum = ruleSet.LastValidatedChecksum ?? ruleSet.PublishedChecksum
        });
    }

    public static string Moderation(
        ModerationRequest request,
        string action,
        long itemCount,
        long aiCallCount,
        long aiFailureCount)
    {
        return Serialize(new
        {
            requestId = request.Id,
            applicationId = request.ApplicationId,
            action,
            status = request.ProcessingStatus.ToString(),
            itemCount,
            aiCallCount,
            aiFailureCount
        });
    }

    private static string Serialize(object value)
    {
        return JsonSerializer.Serialize(value);
    }
}
