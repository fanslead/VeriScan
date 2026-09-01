using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using VeriScan.Domain.Entities;

namespace VeriScan.Infrastructure.ExternalAi;

internal static class ExternalAiSchemaArtifact
{
    public const string SchemaName = "veriscan_moderation";

    public const string OpenAiChatAdapterContractVersion = "openai-chat-completions-adapter@1";

    public const string OpenAiResponsesAdapterContractVersion = "openai-responses-adapter@1";

    public const string AnthropicMessagesAdapterContractVersion = "anthropic-messages-adapter@1";

    public const string CanonicalSchemaVersion = "moderation-output@1";

    public const string OpenAiChatSchemaTransformerVersion = "openai-chat-wire-common-subset@1";

    public const string OpenAiResponsesSchemaTransformerVersion = "openai-responses-wire-common-subset@1";

    public const string AnthropicMessagesSchemaTransformerVersion = "anthropic-messages-wire-common-subset@1";

    private const string SchemaJson = """
        {
          "$schema": "https://json-schema.org/draft/2020-12/schema",
          "type": "object",
          "additionalProperties": false,
          "properties": {
            "label": {
              "type": "string",
              "enum": ["safe", "unsafe", "review"]
            },
            "reasonCodes": {
              "type": "array",
              "items": {
                "type": "string",
                "pattern": "^[A-Z][A-Z0-9_]{0,63}$"
              },
              "maxItems": 16
            },
            "categories": {
              "type": "array",
              "maxItems": 16,
              "items": {
                "type": "object",
                "additionalProperties": false,
                "properties": {
                  "code": { "type": "string", "pattern": "^[a-z][a-z0-9_]{0,63}$" },
                  "severity": { "type": "string", "enum": ["low", "medium", "high"] }
                },
                "required": ["code", "severity"]
              }
            },
            "evidence": {
              "type": "array",
              "items": {
                "type": "object",
                "additionalProperties": false,
                "required": ["quote"],
                "properties": {
                  "quote": { "type": "string", "minLength": 1, "maxLength": 256 }
                }
              },
              "maxItems": 8
            }
          },
          "required": ["label", "categories", "reasonCodes", "evidence"]
        }
        """;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.Default
    };

    public static JsonNode CreateSchemaNode()
    {
        return JsonNode.Parse(SchemaJson)?.DeepClone()
            ?? throw new InvalidOperationException("审核输出 JSON Schema 初始化失败。");
    }

    public static JsonNode CreateEffectiveSchemaNode(AiProtocol protocol)
    {
        EnsureSupported(protocol);
        return RemoveUnsupportedWireKeywords(CreateSchemaNode());
    }

    public static string GetAdapterContractVersion(AiProtocol protocol)
    {
        return protocol switch
        {
            AiProtocol.OpenAiChatCompletions => OpenAiChatAdapterContractVersion,
            AiProtocol.OpenAiResponses => OpenAiResponsesAdapterContractVersion,
            AiProtocol.AnthropicMessages => AnthropicMessagesAdapterContractVersion,
            _ => throw new ExternalAiConfigurationException("未支持的外部 AI 协议。")
        };
    }

    public static string GetSchemaTransformerVersion(AiProtocol protocol)
    {
        return protocol switch
        {
            AiProtocol.OpenAiChatCompletions => OpenAiChatSchemaTransformerVersion,
            AiProtocol.OpenAiResponses => OpenAiResponsesSchemaTransformerVersion,
            AiProtocol.AnthropicMessages => AnthropicMessagesSchemaTransformerVersion,
            _ => throw new ExternalAiConfigurationException("未支持的外部 AI 协议。")
        };
    }

    public static string GetCanonicalSchemaHash()
    {
        return ComputeSha256(CreateSchemaNode());
    }

    public static string GetEffectiveSchemaHash(AiProtocol protocol)
    {
        return ComputeSha256(CreateEffectiveSchemaNode(protocol));
    }

    public static string Serialize(JsonObject body)
    {
        return body.ToJsonString(JsonOptions);
    }

    private static void EnsureSupported(AiProtocol protocol)
    {
        if (protocol is not AiProtocol.OpenAiChatCompletions and
            not AiProtocol.OpenAiResponses and
            not AiProtocol.AnthropicMessages)
        {
            throw new ExternalAiConfigurationException("未支持的外部 AI 协议。");
        }
    }

    private static JsonNode RemoveUnsupportedWireKeywords(JsonNode node)
    {
        return node switch
        {
            JsonObject source => TransformObject(source),
            JsonArray source => TransformArray(source),
            _ => node.DeepClone()
        };
    }

    private static JsonObject TransformObject(JsonObject source)
    {
        var transformed = new JsonObject();
        foreach (var property in source)
        {
            if (property.Key is "$schema" or "pattern" or "minLength" or "maxLength" or "maxItems")
            {
                continue;
            }

            transformed[property.Key] = property.Value is null
                ? null
                : RemoveUnsupportedWireKeywords(property.Value);
        }

        return transformed;
    }

    private static JsonArray TransformArray(JsonArray source)
    {
        var transformed = new JsonArray();
        foreach (var item in source)
        {
            transformed.Add(item is null ? null : RemoveUnsupportedWireKeywords(item));
        }

        return transformed;
    }

    private static string ComputeSha256(JsonNode node)
    {
        var json = node.ToJsonString(JsonOptions);
        var hash = System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
