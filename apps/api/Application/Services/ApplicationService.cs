using VeriScan.Application.Abstractions;
using VeriScan.Application.Contracts;
using VeriScan.Domain.Entities;

namespace VeriScan.Application.Services;

public interface IApplicationService
{
    Task<ApplicationResponse> CreateAsync(CreateApplicationRequest request, CancellationToken cancellationToken);

    Task<ApplicationListResponse> ListAsync(CancellationToken cancellationToken);

    Task<ApplicationResponse> GetAsync(Guid applicationId, CancellationToken cancellationToken);

    Task<ApplicationResponse> UpdateAsync(
        Guid applicationId,
        UpdateApplicationRequest request,
        CancellationToken cancellationToken);

    Task<ApplicationResponse> BindRuleSetAsync(
        Guid applicationId,
        BindApplicationRuleSetRequest request,
        CancellationToken cancellationToken);
}

public sealed class ApplicationService(
    IApplicationStore applicationStore,
    IRuleSetStore ruleSetStore,
    IApiKeyCacheInvalidator cacheInvalidator,
    IOperationalFactService operationalFactService) : IApplicationService
{
    public async Task<ApplicationResponse> CreateAsync(
        CreateApplicationRequest request,
        CancellationToken cancellationToken)
    {
        var publicId = $"app_{Guid.CreateVersion7():N}";
        var defaultRuleSet = await ruleSetStore.GetLatestPublishedAsync(cancellationToken);
        var application = new ApplicationEntity(
            Guid.Empty,
            publicId,
            request.Name.Trim(),
            request.Environment,
            defaultRuleSet?.Id);

        await applicationStore.AddAsync(application, cancellationToken);
        var afterJson = OperationalFactPayloads.Application(application, "created");
        await operationalFactService.RecordAuditAsync(
            new AuditEntry(
                application.TenantId,
                application.Id,
                null,
                "admin",
                null,
                "application.created",
                "application",
                application.Id.ToString(),
                null,
                afterJson,
                null,
                application.CreatedAt),
            cancellationToken);
        await operationalFactService.EnqueueAsync(
            new OutboxMessage(
                "application.created",
                "application",
                application.Id,
                application.TenantId,
                application.Id,
                afterJson,
                application.CreatedAt),
            cancellationToken);
        await applicationStore.SaveChangesAsync(cancellationToken);

        return ApplicationMappings.ToResponseWithRuleSet(application, defaultRuleSet, 0);
    }

    public async Task<ApplicationListResponse> ListAsync(CancellationToken cancellationToken)
    {
        var applications = await applicationStore.ListAsync(cancellationToken);
        var items = applications
            .Select(application => ApplicationMappings.ToResponse(
                application,
                application.ApiKeys.Count(key => key.Status == ApiKeyStatus.Active)))
            .ToArray();

        return new ApplicationListResponse(items, items.Length);
    }

    public async Task<ApplicationResponse> GetAsync(Guid applicationId, CancellationToken cancellationToken)
    {
        var application = await applicationStore.GetByIdAsync(applicationId, cancellationToken)
            ?? throw new ResourceNotFoundException("应用不存在。");

        return ApplicationMappings.ToResponse(
            application,
            application.ApiKeys.Count(key => key.Status == ApiKeyStatus.Active));
    }

    public async Task<ApplicationResponse> UpdateAsync(
        Guid applicationId,
        UpdateApplicationRequest request,
        CancellationToken cancellationToken)
    {
        var application = await applicationStore.GetByIdAsync(applicationId, cancellationToken)
            ?? throw new ResourceNotFoundException("应用不存在。");
        var beforeJson = OperationalFactPayloads.Application(application, "before_update");

        if (request.Name is not null)
        {
            application.Rename(request.Name.Trim());
        }

        if (request.Status.HasValue)
        {
            switch (request.Status.Value)
            {
                case ApplicationStatus.Active:
                    application.Activate();
                    break;
                case ApplicationStatus.Suspended:
                    application.Suspend();
                    break;
                case ApplicationStatus.Archived:
                    application.Archive();
                    break;
                default:
                    throw new RequestValidationException("应用状态无效。");
            }
        }

        var afterJson = OperationalFactPayloads.Application(application, "updated");
        await operationalFactService.RecordAuditAsync(
            new AuditEntry(
                application.TenantId,
                application.Id,
                null,
                "admin",
                null,
                "application.updated",
                "application",
                application.Id.ToString(),
                beforeJson,
                afterJson,
                null,
                application.UpdatedAt),
            cancellationToken);
        await operationalFactService.EnqueueAsync(
            new OutboxMessage(
                "application.updated",
                "application",
                application.Id,
                application.TenantId,
                application.Id,
                afterJson,
                application.UpdatedAt),
            cancellationToken);
        await applicationStore.SaveChangesAsync(cancellationToken);
        if (request.Status.HasValue)
        {
            await cacheInvalidator.InvalidateManyAsync(
                application.ApiKeys.Select(key => key.PublicKeyId).ToArray(),
                cancellationToken);
        }

        return ApplicationMappings.ToResponse(
            application,
            application.ApiKeys.Count(key => key.Status == ApiKeyStatus.Active));
    }

    public async Task<ApplicationResponse> BindRuleSetAsync(
        Guid applicationId,
        BindApplicationRuleSetRequest request,
        CancellationToken cancellationToken)
    {
        var application = await applicationStore.GetByIdAsync(applicationId, cancellationToken)
            ?? throw new ResourceNotFoundException("应用不存在。");
        var ruleSet = await ruleSetStore.GetByPublicRevisionIdAsync(
            request.PublicRevisionId.Trim(),
            cancellationToken)
            ?? throw new ResourceNotFoundException("规则集版本不存在。");
        if (ruleSet.Status != RuleSetStatus.Published)
        {
            throw new RequestConflictException("应用只能绑定已发布的规则集版本。");
        }

        if (application.RuleSetVersionId == ruleSet.Id)
        {
            return ApplicationMappings.ToResponseWithRuleSet(
                application,
                ruleSet,
                application.ApiKeys.Count(key => key.Status == ApiKeyStatus.Active));
        }

        var changedAt = DateTimeOffset.UtcNow;
        var beforeJson = OperationalFactPayloads.Application(application, "before_bind_rule_set");
        application.RuleSetVersion?.RecordBindingChange(changedAt);
        ruleSet.RecordBindingChange(changedAt);
        application.BindRuleSet(ruleSet.Id, changedAt);
        var afterJson = OperationalFactPayloads.Application(application, "bound_rule_set");
        await operationalFactService.RecordAuditAsync(
            new AuditEntry(
                application.TenantId,
                application.Id,
                null,
                "admin",
                null,
                "application.rule_set_bound",
                "application",
                application.Id.ToString(),
                beforeJson,
                afterJson,
                null,
                changedAt),
            cancellationToken);
        await operationalFactService.EnqueueAsync(
            new OutboxMessage(
                "application.rule_set_bound",
                "application",
                application.Id,
                application.TenantId,
                application.Id,
                afterJson,
                changedAt),
            cancellationToken);
        try
        {
            await applicationStore.SaveChangesAsync(cancellationToken);
        }
        catch (DataConcurrencyException)
        {
            throw new RequestConflictException("规则集状态或应用绑定已被其他请求修改，请刷新后重试。");
        }

        return ApplicationMappings.ToResponseWithRuleSet(
            application,
            ruleSet,
            application.ApiKeys.Count(key => key.Status == ApiKeyStatus.Active));
    }
}
