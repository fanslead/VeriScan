using VeriScan.Api.Authentication;
using VeriScan.Application.Contracts;
using VeriScan.Application.Services;

namespace VeriScan.Api.Endpoints;

/// <summary>应用用量统计管理接口。</summary>
public static class ApplicationUsageEndpoints
{
    public static IEndpointRouteBuilder MapApplicationUsageEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/admin/v1/applications")
            .WithTags("Application Usage")
            .RequireAuthorization(AdminJwtOptions.Policy);

        group.MapGet("/{applicationId:guid}/usage", async (
                Guid applicationId,
                [AsParameters] ApplicationUsageQuery query,
                IApplicationUsageService service,
                CancellationToken cancellationToken) =>
            {
                var response = await service.GetAsync(applicationId, query, cancellationToken);
                return TypedResults.Ok(response);
            })
            .WithName("GetApplicationUsage")
            .WithSummary("查询应用用量")
            .WithDescription("按应用或 API Key 查询审核请求、内容决定和已记录的外部 AI 用量。")
            .Produces<ApplicationUsageResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return endpoints;
    }
}
