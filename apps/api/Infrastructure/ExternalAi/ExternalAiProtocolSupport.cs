using System.Text;
using System.Text.Json.Nodes;
using VeriScan.Application.Abstractions;
using VeriScan.Domain.Entities;

namespace VeriScan.Infrastructure.ExternalAi;

internal class ExternalAiConfigurationException(string message) : Exception(message);

internal sealed class ExternalAiInputTooLargeException(string message) : ExternalAiConfigurationException(message);

internal static class ExternalAiProtocolSupport
{
    public static readonly HttpRequestOptionsKey<TimeSpan> ConnectTimeoutOption =
        new("VeriScan.ExternalAi.ConnectTimeout");

    public static Uri GetEndpoint(AiModelConfiguration configuration)
    {
        return new Uri(new Uri(configuration.BaseUrl, UriKind.Absolute), configuration.EndpointPath);
    }

    public static HttpRequestMessage CreateRequest(
        Uri endpoint,
        AiModelConfiguration configuration,
        string credential,
        JsonObject body,
        bool anthropic)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, AddApiVersion(endpoint, configuration, anthropic))
        {
            Content = new StringContent(ExternalAiSchemaArtifact.Serialize(body), Encoding.UTF8, "application/json")
        };
        request.Options.Set(
            ConnectTimeoutOption,
            TimeSpan.FromMilliseconds(Math.Clamp(configuration.ConnectTimeoutMs, 100, 30_000)));
        request.Headers.Accept.ParseAdd("application/json");

        switch (configuration.AuthScheme)
        {
            case AiAuthScheme.Bearer:
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", credential);
                break;
            case AiAuthScheme.XApiKey:
                request.Headers.TryAddWithoutValidation("x-api-key", credential);
                break;
            case AiAuthScheme.ApiKey:
                request.Headers.TryAddWithoutValidation("api-key", credential);
                break;
            default:
                throw new InvalidOperationException("未支持的外部 AI 认证方式。");
        }

        if (anthropic && configuration.ApiVersionLocation == AiApiVersionLocation.Header)
        {
            request.Headers.TryAddWithoutValidation(
                "anthropic-version",
                configuration.ApiVersion!);
        }

        return request;
    }

    public static int EstimateInputTokens(AiModelConfiguration configuration, AiModerationRequest request)
    {
        var scalarCount = configuration.SystemPrompt.EnumerateRunes().Count() +
                          request.Content.EnumerateRunes().Count() +
                          16;
        var segmentedEstimate = 16;
        foreach (var text in new[] { configuration.SystemPrompt, request.Content })
        {
            var asciiRun = 0;
            foreach (var rune in text.EnumerateRunes())
            {
                if (rune.Value is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9')
                {
                    asciiRun++;
                    continue;
                }

                if (asciiRun > 0)
                {
                    segmentedEstimate += (asciiRun + 3) / 4;
                    asciiRun = 0;
                }

                segmentedEstimate++;
            }

            if (asciiRun > 0)
            {
                segmentedEstimate += (asciiRun + 3) / 4;
            }
        }

        return Math.Max(scalarCount, segmentedEstimate);
    }

    public static void ValidateInputBudget(
        AiModelConfiguration configuration,
        AiModerationRequest request)
    {
        if (EstimateInputTokens(configuration, request) > configuration.MaxInputTokens)
        {
            throw new ExternalAiInputTooLargeException("外部 AI 输入超过当前模型配置的 Token 上限。");
        }
    }

    private static Uri AddApiVersion(Uri endpoint, AiModelConfiguration configuration, bool anthropic)
    {
        if (configuration.ApiVersionLocation == AiApiVersionLocation.None || string.IsNullOrWhiteSpace(configuration.ApiVersion))
        {
            return endpoint;
        }

        if (configuration.ApiVersionLocation == AiApiVersionLocation.Header)
        {
            if (!anthropic)
            {
                throw new ExternalAiConfigurationException("OpenAI 协议不支持通过受控 Header 注入 apiVersion。");
            }

            return endpoint;
        }

        if (anthropic)
        {
            throw new ExternalAiConfigurationException("Messages 协议不支持通过查询参数注入 apiVersion。");
        }

        var builder = new UriBuilder(endpoint)
        {
            Query = $"api-version={Uri.EscapeDataString(configuration.ApiVersion)}"
        };
        return builder.Uri;
    }

    public static void AddTemperature(JsonObject body, AiModelConfiguration configuration)
    {
        if (configuration.DecodingMode == AiDecodingMode.SendTemperatureZero)
        {
            body["temperature"] = 0;
        }
    }

    public static JsonObject CreateOpenAiJsonSchemaFormat()
    {
        return new JsonObject
        {
            ["type"] = "json_schema",
            ["json_schema"] = new JsonObject
            {
                ["name"] = ExternalAiSchemaArtifact.SchemaName,
                ["strict"] = true,
                ["schema"] = ExternalAiSchemaArtifact.CreateEffectiveSchemaNode(AiProtocol.OpenAiChatCompletions)
            }
        };
    }

    public static JsonObject CreateResponsesJsonSchemaFormat()
    {
        return new JsonObject
        {
            ["type"] = "json_schema",
            ["name"] = ExternalAiSchemaArtifact.SchemaName,
            ["strict"] = true,
            ["schema"] = ExternalAiSchemaArtifact.CreateEffectiveSchemaNode(AiProtocol.OpenAiResponses)
        };
    }

    public static JsonObject CreateAnthropicJsonSchemaFormat()
    {
        return new JsonObject
        {
            ["type"] = "json_schema",
            ["schema"] = ExternalAiSchemaArtifact.CreateEffectiveSchemaNode(AiProtocol.AnthropicMessages)
        };
    }
}
