using System.Diagnostics;
using VeriScan.Application.Abstractions;
using VeriScan.Domain.Entities;

namespace VeriScan.Infrastructure.ExternalAi;

public sealed class ExternalAiConfigurationProbe(
    IAiEndpointPolicy endpointPolicy,
    IExternalAiCredentialResolver credentialResolver,
    OpenAiChatCompletionsClient chatCompletionsClient,
    OpenAiResponsesClient responsesClient,
    AnthropicMessagesClient messagesClient) : IAiConfigurationProbe
{
    private const string SyntheticContent = "这是一条用于验证审核配置的合成文本，请仅依据系统提示词返回约定的 JSON 结构。";

    public async Task<AiConfigurationProbeResult> ProbeAsync(
        AiModelConfiguration configuration,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        AiModerationResult result;
        try
        {
            endpointPolicy.Validate(ExternalAiProtocolSupport.GetEndpoint(configuration));
            ExternalAiProtocolSupport.ValidateInputBudget(
                configuration,
                new AiModerationRequest(Guid.Empty, Guid.Empty, SyntheticContent, "zh-CN"));
            if (!credentialResolver.TryResolve(configuration.CredentialRef, out var credential))
            {
                result = ExternalAiResultMapping.Failure(
                    configuration,
                    AiModerationOutcome.Unavailable,
                    "AI_CREDENTIAL_NOT_FOUND");
            }
            else
            {
                var request = new AiModerationRequest(Guid.Empty, Guid.Empty, SyntheticContent, "zh-CN");
                result = configuration.Protocol switch
                {
                    AiProtocol.OpenAiChatCompletions => await chatCompletionsClient.ModerateAsync(
                        configuration,
                        request,
                        credential,
                        cancellationToken),
                    AiProtocol.OpenAiResponses => await responsesClient.ModerateAsync(
                        configuration,
                        request,
                        credential,
                        cancellationToken),
                    AiProtocol.AnthropicMessages => await messagesClient.ModerateAsync(
                        configuration,
                        request,
                        credential,
                        cancellationToken),
                    _ => ExternalAiResultMapping.Failure(
                        configuration,
                        AiModerationOutcome.Unavailable,
                        "AI_PROTOCOL_UNSUPPORTED")
                };
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            result = ExternalAiResultMapping.Failure(
                configuration,
                AiModerationOutcome.Unavailable,
                "AI_TIMEOUT");
        }
        catch (RequestValidationException)
        {
            result = ExternalAiResultMapping.Failure(
                configuration,
                AiModerationOutcome.PolicyDenied,
                "AI_EXTERNAL_POLICY_DENIED");
        }
        catch (ExternalAiInputTooLargeException)
        {
            result = ExternalAiResultMapping.Failure(
                configuration,
                AiModerationOutcome.PolicyDenied,
                "AI_INPUT_TOO_LARGE");
        }
        catch (ExternalAiConfigurationException)
        {
            result = ExternalAiResultMapping.Failure(
                configuration,
                AiModerationOutcome.PolicyDenied,
                "AI_CONFIG_INVALID");
        }
        catch (Exception) when (cancellationToken.IsCancellationRequested == false)
        {
            result = ExternalAiResultMapping.Failure(
                configuration,
                AiModerationOutcome.Unavailable,
                "AI_ADAPTER_ERROR");
        }

        stopwatch.Stop();
        return new AiConfigurationProbeResult(
            result.Outcome == AiModerationOutcome.Succeeded,
            configuration.Protocol.ToString(),
            configuration.Model,
            stopwatch.ElapsedMilliseconds,
            result.InputTokens,
            result.OutputTokens,
            result.FailureCode ?? MapFailureCode(result.Outcome));
    }

    private static string? MapFailureCode(AiModerationOutcome outcome)
    {
        return outcome switch
        {
            AiModerationOutcome.Succeeded => null,
            AiModerationOutcome.ProviderRefusal => "AI_PROVIDER_REFUSAL",
            AiModerationOutcome.Truncated => "AI_OUTPUT_TRUNCATED",
            AiModerationOutcome.InvalidOutput => "AI_OUTPUT_INVALID",
            AiModerationOutcome.PolicyDenied => "AI_EXTERNAL_POLICY_DENIED",
            _ => "AI_UNAVAILABLE"
        };
    }
}
