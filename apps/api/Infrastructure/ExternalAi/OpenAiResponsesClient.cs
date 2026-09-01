using System.Text.Json;
using System.Text.Json.Nodes;
using VeriScan.Application.Abstractions;
using VeriScan.Domain.Entities;

namespace VeriScan.Infrastructure.ExternalAi;

public sealed class OpenAiResponsesClient(
    HttpClient httpClient,
    ExternalAiHttpExecutor executor)
{
    public async Task<AiModerationResult> ModerateAsync(
        AiModelConfiguration configuration,
        AiModerationRequest moderationRequest,
        string credential,
        CancellationToken cancellationToken)
    {
        var endpoint = ExternalAiProtocolSupport.GetEndpoint(configuration);
        var body = new JsonObject
        {
            ["model"] = configuration.Model,
            ["instructions"] = configuration.SystemPrompt,
            ["input"] = moderationRequest.Content,
            ["max_output_tokens"] = configuration.MaxOutputTokens,
            ["store"] = false,
            ["text"] = new JsonObject
            {
                ["format"] = ExternalAiProtocolSupport.CreateResponsesJsonSchemaFormat()
            }
        };
        ExternalAiProtocolSupport.AddTemperature(body, configuration);

        var response = await executor.ExecuteAsync(
            httpClient,
            configuration,
            () => ExternalAiProtocolSupport.CreateRequest(endpoint, configuration, credential, body, anthropic: false),
            cancellationToken);
        return ExternalAiResultMapping.FromHttp(
            configuration,
            response,
            body => ParseResponse(body, moderationRequest.Content));
    }

    private static ExternalAiProviderParse ParseResponse(string body, string sourceContent)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !TryReadString(root, "id", out var providerRequestId))
            {
                return Invalid();
            }

            if (TryReadString(root, "status", out var status))
            {
                if (string.Equals(status, "incomplete", StringComparison.Ordinal))
                {
                    return new ExternalAiProviderParse(
                        AiModerationOutcome.Truncated,
                        null,
                        providerRequestId,
                        ReadUsage(root, "input_tokens"),
                        ReadUsage(root, "output_tokens"),
                        "AI_OUTPUT_TRUNCATED");
                }

                if (status is "failed" or "cancelled")
                {
                    return new ExternalAiProviderParse(
                        AiModerationOutcome.Unavailable,
                        null,
                        providerRequestId,
                        ReadUsage(root, "input_tokens"),
                        ReadUsage(root, "output_tokens"),
                        "AI_PROVIDER_FAILED");
                }

                if (!string.Equals(status, "completed", StringComparison.Ordinal))
                {
                    return Invalid(providerRequestId);
                }
            }

            if (!root.TryGetProperty("output", out var output) ||
                output.ValueKind != JsonValueKind.Array)
            {
                return Invalid(providerRequestId);
            }

            var text = new System.Text.StringBuilder();
            var messageCount = 0;
            foreach (var item in output.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object ||
                    !TryReadString(item, "type", out var itemType))
                {
                    return Invalid(providerRequestId);
                }

                if (string.Equals(itemType, "message", StringComparison.Ordinal))
                {
                    messageCount++;
                    if (!item.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array)
                    {
                        return Invalid(providerRequestId);
                    }

                    var outputTextCount = 0;
                    foreach (var contentItem in content.EnumerateArray())
                    {
                        if (contentItem.ValueKind != JsonValueKind.Object ||
                            !TryReadString(contentItem, "type", out var contentType))
                        {
                            return Invalid(providerRequestId);
                        }

                        if (string.Equals(contentType, "refusal", StringComparison.Ordinal))
                        {
                            return Refusal(providerRequestId, root);
                        }

                        if (!string.Equals(contentType, "output_text", StringComparison.Ordinal) ||
                            !TryReadString(contentItem, "text", out var contentText))
                        {
                            return Invalid(providerRequestId);
                        }

                        outputTextCount++;
                        text.Append(contentText);
                    }

                    if (outputTextCount != 1)
                    {
                        return Invalid(providerRequestId);
                    }

                    continue;
                }

                if (string.Equals(itemType, "refusal", StringComparison.Ordinal))
                {
                    return Refusal(providerRequestId, root);
                }

                if (string.Equals(itemType, "reasoning", StringComparison.Ordinal))
                {
                    continue;
                }

                return Invalid(providerRequestId);
            }

            if (messageCount != 1 || !ExternalAiWire.TryParseCanonical(text.ToString(), sourceContent, out var canonical))
            {
                return Invalid(providerRequestId);
            }

            return new ExternalAiProviderParse(
                AiModerationOutcome.Succeeded,
                canonical,
                providerRequestId,
                ReadUsage(root, "input_tokens"),
                ReadUsage(root, "output_tokens"),
                null);
        }
        catch (JsonException)
        {
            return Invalid();
        }
    }

    private static ExternalAiProviderParse Refusal(string? requestId, JsonElement root)
    {
        return new ExternalAiProviderParse(
            AiModerationOutcome.ProviderRefusal,
            null,
            requestId,
            ReadUsage(root, "input_tokens"),
            ReadUsage(root, "output_tokens"),
            "AI_PROVIDER_REFUSAL");
    }

    private static ExternalAiProviderParse Invalid(string? requestId = null)
    {
        return new ExternalAiProviderParse(
            AiModerationOutcome.InvalidOutput,
            null,
            requestId,
            null,
            null,
            "AI_OUTPUT_INVALID");
    }

    private static int? ReadUsage(JsonElement root, string propertyName)
    {
        return root.TryGetProperty("usage", out var usage) && usage.ValueKind == JsonValueKind.Object &&
               usage.TryGetProperty(propertyName, out var value) && value.TryGetInt32(out var tokens)
            ? tokens
            : null;
    }

    private static bool TryReadString(JsonElement element, string propertyName, out string? value)
    {
        value = null;
        return element.TryGetProperty(propertyName, out var property) &&
               property.ValueKind == JsonValueKind.String &&
               (value = property.GetString()) is not null;
    }
}
