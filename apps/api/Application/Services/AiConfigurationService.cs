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
    private readonly IAiCredentialProtector credentialProtector;
    private readonly IOperationalFactService operationalFactService;

    public AiConfigurationService(
        IAiModelConfigurationStore store,
        IAiEndpointPolicy endpointPolicy,
        IAiConfigurationProbe probe,
        IAiSchemaDescriptor schemaDescriptor,
        IAiCredentialProtector credentialProtector,
        IOperationalFactService operationalFactService)
    {
        this.store = store;
        this.endpointPolicy = endpointPolicy;
        this.probe = probe;
        this.schemaDescriptor = schemaDescriptor;
        this.credentialProtector = credentialProtector;
        this.operationalFactService = operationalFactService;
    }

    public async Task<AiConfigurationResponse> CreateAsync(
        CreateAiConfigurationRequest request,
        CancellationToken cancellationToken)
    {
        var draft = Validate(request);
        var configuration = CreateEntity(draft);
        ApplyCredential(configuration, draft);
        await store.AddAsync(configuration, cancellationToken);
        await RecordChangeAsync(configuration, "ai_configuration.created", null, cancellationToken);
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
        configuration.CopyCredentialFrom(source);
        await store.AddAsync(configuration, cancellationToken);
        await RecordChangeAsync(configuration, "ai_configuration.revision_created", null, cancellationToken);
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

        var beforeJson = OperationalFactPayloads.AiConfiguration(configuration, "before_update");
        var draft = Validate(request, configuration);
        ApplyDraft(configuration, draft);
        ApplyCredential(configuration, draft);
        await RecordChangeAsync(configuration, "ai_configuration.updated", beforeJson, cancellationToken);
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

        var beforeJson = OperationalFactPayloads.AiConfiguration(configuration, "before_publish");
        ValidateEndpoint(configuration);
        var schema = schemaDescriptor.Describe(configuration.Protocol);
        configuration.Publish(
            schema.AdapterContractVersion,
            schema.CanonicalSchemaVersion,
            schema.CanonicalSchemaHash,
            schema.EffectiveSchemaHash,
            schema.SchemaTransformerVersion,
            DateTimeOffset.UtcNow);
        await RecordChangeAsync(configuration, "ai_configuration.published", beforeJson, cancellationToken);
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

        var beforeJson = OperationalFactPayloads.AiConfiguration(configuration, "before_activate");
        await RecordChangeAsync(configuration, "ai_configuration.activated", beforeJson, cancellationToken);
        await store.ActivateExclusiveAsync(configuration, DateTimeOffset.UtcNow, cancellationToken);
        return AiConfigurationMappings.ToResponse(configuration);
    }

    public async Task<AiConfigurationResponse> ArchiveAsync(Guid id, CancellationToken cancellationToken)
    {
        var configuration = await GetRequiredAsync(id, cancellationToken);
        var beforeJson = OperationalFactPayloads.AiConfiguration(configuration, "before_archive");
        configuration.Archive(DateTimeOffset.UtcNow);
        await RecordChangeAsync(configuration, "ai_configuration.archived", beforeJson, cancellationToken);
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
        var beforeJson = OperationalFactPayloads.AiConfiguration(configuration, "before_test");
        configuration.RecordTestResult(result.Succeeded, result.FailureCode, DateTimeOffset.UtcNow);
        await RecordChangeAsync(configuration, "ai_configuration.tested", beforeJson, cancellationToken);
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

    private async Task RecordChangeAsync(
        AiModelConfiguration configuration,
        string action,
        string? beforeJson,
        CancellationToken cancellationToken)
    {
        var afterJson = OperationalFactPayloads.AiConfiguration(configuration, action);
        await operationalFactService.RecordAuditAsync(
            new AuditEntry(
                null,
                null,
                null,
                "admin",
                null,
                action,
                "ai_configuration",
                configuration.Id.ToString(),
                beforeJson,
                afterJson,
                null,
                configuration.UpdatedAt),
            cancellationToken);
        await operationalFactService.EnqueueAsync(
            new OutboxMessage(
                action,
                "ai_configuration",
                configuration.Id,
                null,
                null,
                afterJson,
                configuration.UpdatedAt),
            cancellationToken);
    }
}
