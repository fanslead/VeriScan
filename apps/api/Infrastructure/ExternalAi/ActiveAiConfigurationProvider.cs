using Microsoft.Extensions.DependencyInjection;
using VeriScan.Application.Abstractions;
using VeriScan.Domain.Entities;

namespace VeriScan.Infrastructure.ExternalAi;

public sealed class ActiveAiConfigurationProvider(IServiceScopeFactory scopeFactory)
    : IActiveAiConfigurationProvider
{
    public async Task<AiModelConfiguration?> GetActiveAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IAiModelConfigurationStore>();
        return await store.GetActiveAsync(cancellationToken);
    }
}
