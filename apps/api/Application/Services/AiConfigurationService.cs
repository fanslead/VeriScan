using VeriScan.Application.Abstractions;
using VeriScan.Application.Contracts;
using VeriScan.Domain.Entities;

namespace VeriScan.Application.Services;

public interface IAiConfigurationService
{
    Task<AiConfigurationResponse> CreateAsync(
        CreateAiConfigurationRequest request,
        CancellationToken cancellationToken);

    Task<AiConfigurationListResponse> ListAsync(CancellationToken cancellationToken);

    Task<AiConfigurationResponse> CreateRevisionAsync(Guid sourceId, CancellationToken cancellationToken);

    Task<AiConfigurationResponse> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<AiConfigurationResponse> UpdateAsync(
        Guid id,
        AiConfigurationDraftRequest request,
        CancellationToken cancellationToken);

    Task<AiConfigurationResponse> PublishAsync(Guid id, CancellationToken cancellationToken);

    Task<AiConfigurationResponse> ActivateAsync(Guid id, CancellationToken cancellationToken);

    Task<AiConfigurationResponse> ArchiveAsync(Guid id, CancellationToken cancellationToken);

    Task<AiConfigurationTestResponse> TestAsync(Guid id, CancellationToken cancellationToken);
}

public sealed partial class AiConfigurationService : IAiConfigurationService
{
    private readonly IAiEndpointPolicy endpointPolicy;
    private readonly IAiConfigurationProbe probe;
    private readonly IAiSchemaDescriptor schemaDescriptor;
    private readonly IAiModelConfigurationStore store;

    public AiConfigurationService(
        IAiModelConfigurationStore store,
        IAiEndpointPolicy endpointPolicy,
        IAiConfigurationProbe probe,
        IAiSchemaDescriptor schemaDescriptor)
    {
        this.store = store;
        this.endpointPolicy = endpointPolicy;
        this.probe = probe;
        this.schemaDescriptor = schemaDescriptor;
    }

    public async Task<AiConfigurationResponse> CreateAsync(
        CreateAiConfigurationRequest request,
        CancellationToken cancellationToken)
    {
        var draft = Validate(request);
        var configuration = CreateEntity(draft);
        await store.AddAsync(configuration, cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
        return AiConfigurationMappings.ToResponse(configuration);
    }

    public async Task<AiConfigurationListResponse> ListAsync(CancellationToken cancellationToken)
    {
        var configurations = await store.ListAsync(cancellationToken);
        return new AiConfigurationListResponse(configurations.Select(AiConfigurationMappings.ToResponse).ToArray());
    }

    public async Task<AiConfigurationResponse> CreateRevisionAsync(
        Guid sourceId,
        CancellationToken cancellationToken)
    {
        var source = await GetRequiredAsync(sourceId, cancellationToken);
        var configuration = new AiModelConfiguration(
            source.Name,
            source.Protocol,
            source.BaseUrl,
            source.EndpointPath,
            source.CredentialRef,
            source.AuthScheme,
            source.Model,
            source.ApiVersion,
            source.ApiVersionLocation,
            source.SystemPrompt,
            source.DecodingMode,
            source.MaxInputTokens,
            source.MaxOutputTokens,
            source.ConnectTimeoutMs,
            source.RequestTimeoutMs,
            source.MaxAttempts,
            source.DataRegion,
            source.RetentionClass);
        await store.AddAsync(configuration, cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
        return AiConfigurationMappings.ToResponse(configuration);
    }

    public async Task<AiConfigurationResponse> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        return AiConfigurationMappings.ToResponse(await GetRequiredAsync(id, cancellationToken));
    }

    public async Task<AiConfigurationResponse> UpdateAsync(
        Guid id,
        AiConfigurationDraftRequest request,
        CancellationToken cancellationToken)
    {
        var configuration = await GetRequiredAsync(id, cancellationToken);
        if (configuration.Status != AiConfigurationStatus.Draft)
        {
            throw new RequestConflictException("已发布或已归档的 AI 配置不可原地修改，请创建新草稿。");
        }

        var draft = Validate(request);
        ApplyDraft(configuration, draft);
        await store.SaveChangesAsync(cancellationToken);
        return AiConfigurationMappings.ToResponse(configuration);
    }

    public async Task<AiConfigurationResponse> PublishAsync(Guid id, CancellationToken cancellationToken)
    {
        var configuration = await GetRequiredAsync(id, cancellationToken);
        if (configuration.Status != AiConfigurationStatus.Draft)
        {
            throw new RequestConflictException("只有草稿状态的 AI 配置可以发布。");
        }


        if (configuration.LastTestSucceeded != true ||
            configuration.LastTestedAt is null ||
            configuration.LastTestedAt < configuration.UpdatedAt)
        {
            throw new RequestConflictException("发布前必须对当前草稿执行一次成功的合成连接测试。");
        }

        ValidateEndpoint(configuration);
        var schema = schemaDescriptor.Describe(configuration.Protocol);
        configuration.Publish(
            schema.AdapterContractVersion,
            schema.CanonicalSchemaVersion,
            schema.CanonicalSchemaHash,
            schema.EffectiveSchemaHash,
            schema.SchemaTransformerVersion,
            DateTimeOffset.UtcNow);
        await store.SaveChangesAsync(cancellationToken);
        return AiConfigurationMappings.ToResponse(configuration);
    }

    public async Task<AiConfigurationResponse> ActivateAsync(Guid id, CancellationToken cancellationToken)
    {
        var configuration = await GetRequiredAsync(id, cancellationToken);
        if (configuration.Status != AiConfigurationStatus.Published)
        {
            throw new RequestConflictException("只有已发布的 AI 配置可以激活。");
        }

        await store.ActivateExclusiveAsync(configuration, DateTimeOffset.UtcNow, cancellationToken);
        return AiConfigurationMappings.ToResponse(configuration);
    }

    public async Task<AiConfigurationResponse> ArchiveAsync(Guid id, CancellationToken cancellationToken)
    {
        var configuration = await GetRequiredAsync(id, cancellationToken);
        configuration.Archive(DateTimeOffset.UtcNow);
        await store.SaveChangesAsync(cancellationToken);
        return AiConfigurationMappings.ToResponse(configuration);
    }

    public async Task<AiConfigurationTestResponse> TestAsync(Guid id, CancellationToken cancellationToken)
    {
        var configuration = await GetRequiredAsync(id, cancellationToken);
        if (configuration.Status == AiConfigurationStatus.Archived)
        {
            throw new RequestConflictException("已归档的 AI 配置不能执行连接测试。");
        }

        ValidateEndpoint(configuration);
        var result = await probe.ProbeAsync(configuration, cancellationToken);
        configuration.RecordTestResult(result.Succeeded, result.FailureCode, DateTimeOffset.UtcNow);
        await store.SaveChangesAsync(cancellationToken);
        return new AiConfigurationTestResponse(
            result.Succeeded,
            result.Protocol,
            result.Model,
            result.LatencyMs,
            result.InputTokens,
            result.OutputTokens,
            result.FailureCode);
    }

    private async Task<AiModelConfiguration> GetRequiredAsync(Guid id, CancellationToken cancellationToken)
    {
        return await store.GetByIdAsync(id, cancellationToken)
            ?? throw new ResourceNotFoundException("AI 模型配置不存在。");
    }
}
