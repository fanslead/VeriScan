namespace VeriScan.Domain.Entities;

public enum AiProtocol
{
    OpenAiChatCompletions,
    OpenAiResponses,
    AnthropicMessages
}

public enum AiAuthScheme
{
    Bearer,
    XApiKey,
    ApiKey
}

public enum AiDecodingMode
{
    SendTemperatureZero,
    OmitTemperature,
    ProviderFixed
}

public enum AiApiVersionLocation
{
    None,
    Header,
    Query
}

public enum AiConfigurationStatus
{
    Draft,
    Published,
    Archived
}
