using System.Text.Json;
using System.Text.Json.Nodes;
using VeriScan.Application.Abstractions;
using VeriScan.Domain.Entities;

namespace VeriScan.Infrastructure.ExternalAi;

public sealed class OpenAiChatCompletionsClient(
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
            ["messages"] = new JsonArray
            {
                new JsonObject
                {
                    ["role"] = "system",
                    ["content"] = configuration.SystemPrompt
                },
                new JsonObject
                {
                    ["role"] = "user",
                    ["content"] = moderationRequest.Content
                }
            },
            ["max_completion_tokens"] = configuration.MaxOutputTokens,
            ["response_format"] = ExternalAiProtocolSupport.CreateOpenAiJsonSchemaFormat()
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
                !TryReadString(root, "id", out var providerRequestId) ||
                !root.TryGetProperty("choices", out var choices) ||
                choices.ValueKind != JsonValueKind.Array || choices.GetArrayLength() != 1)
            {
                return Invalid();
            }

            var choice = choices[0];
            if (choice.ValueKind != JsonValueKind.Object ||
                !TryReadString(choice, "finish_reason", out var finishReason))
            {
                return Invalid(providerRequestId);
            }

            if (string.Equals(finishReason, "length", StringComparison.Ordinal))
            {
                return new ExternalAiProviderParse(
                    AiModerationOutcome.Truncated,
                    null,
                    providerRequestId,
                    ReadUsage(root, "prompt_tokens"),
                    ReadUsage(root, "completion_tokens"),
                    "AI_OUTPUT_TRUNCATED");
            }

            if (string.Equals(finishReason, "content_filter", StringComparison.Ordinal))
            {
                return Refusal(providerRequestId, root);
            }

            if (!string.Equals(finishReason, "stop", StringComparison.Ordinal) ||
                !choice.TryGetProperty("message", out var message) ||
                message.ValueKind != JsonValueKind.Object)
            {
                return Invalid(providerRequestId);
            }

            if (message.TryGetProperty("refusal", out var refusal) && refusal.ValueKind == JsonValueKind.String)
            {
                return Refusal(providerRequestId, root);
            }

            if (!TryReadString(message, "content", out var content) ||
                !ExternalAiWire.TryParseCanonical(content, sourceContent, out var canonical))
            {
                return Invalid(providerRequestId);
            }

            return new ExternalAiProviderParse(
                AiModerationOutcome.Succeeded,
                canonical,
                providerRequestId,
                ReadUsage(root, "prompt_tokens"),
                ReadUsage(root, "completion_tokens"),
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
            ReadUsage(root, "prompt_tokens"),
            ReadUsage(root, "completion_tokens"),
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
