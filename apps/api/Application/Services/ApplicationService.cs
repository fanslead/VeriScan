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
}

public sealed class ApplicationService(
    IApplicationStore applicationStore,
    IApiKeyCacheInvalidator cacheInvalidator) : IApplicationService
{
    public async Task<ApplicationResponse> CreateAsync(
        CreateApplicationRequest request,
        CancellationToken cancellationToken)
    {
        var publicId = $"app_{Guid.CreateVersion7():N}";
        var application = new ApplicationEntity(Guid.Empty, publicId, request.Name.Trim(), request.Environment);

        await applicationStore.AddAsync(application, cancellationToken);
        await applicationStore.SaveChangesAsync(cancellationToken);

        return ApplicationMappings.ToResponse(application, 0);
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
}
