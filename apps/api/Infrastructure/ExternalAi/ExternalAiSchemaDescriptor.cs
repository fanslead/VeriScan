using VeriScan.Application.Abstractions;
using VeriScan.Domain.Entities;

namespace VeriScan.Infrastructure.ExternalAi;

public sealed class ExternalAiSchemaDescriptor : IAiSchemaDescriptor
{
    public AiSchemaDescriptor Describe(AiProtocol protocol)
    {
        return new AiSchemaDescriptor(
            ExternalAiSchemaArtifact.GetAdapterContractVersion(protocol),
            ExternalAiSchemaArtifact.CanonicalSchemaVersion,
            ExternalAiSchemaArtifact.GetCanonicalSchemaHash(),
            ExternalAiSchemaArtifact.GetEffectiveSchemaHash(protocol),
            ExternalAiSchemaArtifact.GetSchemaTransformerVersion(protocol));
    }
}
