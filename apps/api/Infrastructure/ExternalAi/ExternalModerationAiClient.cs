using VeriScan.Application.Abstractions;
using VeriScan.Domain.Entities;

namespace VeriScan.Infrastructure.ExternalAi;

public sealed class ExternalModerationAiClient(
    IAiModelConfigurationStore configurationStore,
    IAiEndpointPolicy endpointPolicy,
    IExternalAiCredentialResolver credentialResolver,
    OpenAiChatCompletionsClient chatCompletionsClient,
    OpenAiResponsesClient responsesClient,
    AnthropicMessagesClient messagesClient) : IModerationAiClient
{
    public async Task<AiModerationResult> ModerateAsync(
        AiModerationRequest request,
        CancellationToken cancellationToken)
    {
        var configuration = await configurationStore.GetActiveAsync(cancellationToken);
        if (configuration is null)
        {
            return new AiModerationResult(
                AiModerationOutcome.NoActiveConfiguration,
                null,
                [],
                [],
                [],
                null,
                null,
                null,
                null,
                "AI_ROUTE_NOT_CONFIGURED");
        }

        if (!TryValidateEndpoint(configuration, out var policyFailure))
        {
            return policyFailure;
        }

        try
        {
            ExternalAiProtocolSupport.ValidateInputBudget(configuration, request);
        }
        catch (ExternalAiConfigurationException)
        {
            return ExternalAiResultMapping.Failure(
                configuration,
                AiModerationOutcome.PolicyDenied,
                "AI_INPUT_TOO_LARGE");
        }

        if (!credentialResolver.TryResolve(configuration.CredentialRef, out var credential))
        {
            return ExternalAiResultMapping.Failure(
                configuration,
                AiModerationOutcome.Unavailable,
                "AI_CREDENTIAL_NOT_FOUND");
        }

        try
        {
            return configuration.Protocol switch
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
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return ExternalAiResultMapping.Failure(
                configuration,
                AiModerationOutcome.Unavailable,
                "AI_TIMEOUT");
        }
        catch (ExternalAiInputTooLargeException)
        {
            return ExternalAiResultMapping.Failure(
                configuration,
                AiModerationOutcome.PolicyDenied,
                "AI_INPUT_TOO_LARGE");
        }
        catch (ExternalAiConfigurationException)
        {
            return ExternalAiResultMapping.Failure(
                configuration,
                AiModerationOutcome.PolicyDenied,
                "AI_CONFIG_INVALID");
        }
        catch (Exception) when (cancellationToken.IsCancellationRequested == false)
        {
            return ExternalAiResultMapping.Failure(
                configuration,
                AiModerationOutcome.Unavailable,
                "AI_ADAPTER_ERROR");
        }
    }

    private bool TryValidateEndpoint(
        AiModelConfiguration configuration,
        out AiModerationResult failure)
    {
        try
        {
            endpointPolicy.Validate(ExternalAiProtocolSupport.GetEndpoint(configuration));
            failure = null!;
            return true;
        }
        catch (Exception)
        {
            failure = ExternalAiResultMapping.Failure(
                configuration,
                AiModerationOutcome.PolicyDenied,
                "AI_EXTERNAL_POLICY_DENIED");
            return false;
        }
    }
}
