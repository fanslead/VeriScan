using VeriScan.Application.Abstractions;
using VeriScan.Domain.Entities;
using VeriScan.Infrastructure.ExternalAi;

namespace VeriScan.Api.Tests;

public sealed class ExternalAiSchemaDescriptorTests
{
    [Fact]
    public void DescribesStableCanonicalAndEffectiveSchemaArtifactsPerProtocol()
    {
        var descriptor = new ExternalAiSchemaDescriptor();
        var protocols = new[]
        {
            AiProtocol.OpenAiChatCompletions,
            AiProtocol.OpenAiResponses,
            AiProtocol.AnthropicMessages
        };

        foreach (var protocol in protocols)
        {
            var first = descriptor.Describe(protocol);
            var second = descriptor.Describe(protocol);

            Assert.Equal(first, second);
            Assert.EndsWith("-adapter@1", first.AdapterContractVersion, StringComparison.Ordinal);
            Assert.Equal("moderation-output@1", first.CanonicalSchemaVersion);
            Assert.Matches("^[0-9a-f]{64}$", first.CanonicalSchemaHash);
            Assert.Matches("^[0-9a-f]{64}$", first.EffectiveSchemaHash);
            Assert.NotEqual(first.CanonicalSchemaHash, first.EffectiveSchemaHash);
            Assert.Equal(
                protocol switch
                {
                    AiProtocol.OpenAiChatCompletions => "openai-chat-wire-common-subset@1",
                    AiProtocol.OpenAiResponses => "openai-responses-wire-common-subset@1",
                    AiProtocol.AnthropicMessages => "anthropic-messages-wire-common-subset@1",
                    _ => throw new InvalidOperationException()
                },
                first.SchemaTransformerVersion);
        }
    }
}
