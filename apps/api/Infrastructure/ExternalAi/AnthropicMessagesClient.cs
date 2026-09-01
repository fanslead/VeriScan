using System.Text.Json;
using System.Text.Json.Nodes;
using VeriScan.Application.Abstractions;
using VeriScan.Domain.Entities;

namespace VeriScan.Infrastructure.ExternalAi;

public sealed class AnthropicMessagesClient(
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
            ["max_tokens"] = configuration.MaxOutputTokens,
            ["system"] = configuration.SystemPrompt,
            ["messages"] = new JsonArray
            {
                new JsonObject
                {
                    ["role"] = "user",
                    ["content"] = moderationRequest.Content
                }
            },
            ["output_config"] = new JsonObject
            {
                ["format"] = ExternalAiProtocolSupport.CreateAnthropicJsonSchemaFormat()
            }
        };
        ExternalAiProtocolSupport.AddTemperature(body, configuration);

        var response = await executor.ExecuteAsync(
            httpClient,
            configuration,
            () => ExternalAiProtocolSupport.CreateRequest(endpoint, configuration, credential, body, anthropic: true),
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
                !TryReadString(root, "id", out var providerRequestId) ||
                (root.TryGetProperty("type", out var type) &&
                 (type.ValueKind != JsonValueKind.String || !string.Equals(type.GetString(), "message", StringComparison.Ordinal))))
            {
                return Invalid();
            }

            if (TryReadString(root, "stop_reason", out var stopReason))
            {
                if (stopReason is "max_tokens" or "model_context_window_exceeded")
                {
                    return new ExternalAiProviderParse(
                        AiModerationOutcome.Truncated,
                        null,
                        providerRequestId,
                        ReadUsage(root, "input_tokens"),
                        ReadUsage(root, "output_tokens"),
                        "AI_OUTPUT_TRUNCATED");
                }

                if (string.Equals(stopReason, "refusal", StringComparison.Ordinal))
                {
                    return Refusal(providerRequestId, root);
                }

                if (stopReason is not "end_turn" and not "stop_sequence")
                {
                    return Invalid(providerRequestId);
                }
            }

            if (!root.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array)
            {
                return Invalid(providerRequestId);
            }

            var text = new System.Text.StringBuilder();
            var textBlockCount = 0;
            foreach (var contentItem in content.EnumerateArray())
            {
                if (contentItem.ValueKind != JsonValueKind.Object ||
                    !TryReadString(contentItem, "type", out var contentType))
                {
                    return Invalid(providerRequestId);
                }

                if (contentType is "thinking" or "redacted_thinking")
                {
                    continue;
                }

                if (!string.Equals(contentType, "text", StringComparison.Ordinal) ||
                    !TryReadString(contentItem, "text", out var contentText))
                {
                    return Invalid(providerRequestId);
                }

                textBlockCount++;
                text.Append(contentText);
            }

            if (textBlockCount != 1 ||
                !ExternalAiWire.TryParseCanonical(text.ToString(), sourceContent, out var canonical))
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
