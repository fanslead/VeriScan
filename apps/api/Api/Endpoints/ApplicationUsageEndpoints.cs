using VeriScan.Api.Authentication;
using VeriScan.Application.Abstractions;
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
            .RequireAuthorization(AdminJwtOptions.Policy, AdminPolicies.Viewer);

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

        group.MapPost("/{applicationId:guid}/usage/rebuild", async (
                Guid applicationId,
                [AsParameters] ApplicationUsageQuery query,
                IUsageProjectionService service,
                CancellationToken cancellationToken) =>
            {
                var result = await service.RebuildAsync(
                    applicationId,
                    query.ApiKeyId,
                    query.From,
                    query.Through,
                    cancellationToken);
                return TypedResults.Ok(new UsageRebuildResponse(
                    applicationId,
                    query.ApiKeyId,
                    result.DataFrom,
                    result.DataThrough,
                    result.HourlyRowsWritten,
                    result.DailyRowsWritten,
                    result.RequestCount,
                    result.ItemCount,
                    result.AiCallCount));
            })
            .RequireAuthorization(AdminPolicies.Auditor)
            .WithName("RebuildApplicationUsage")
            .WithSummary("按事实重建应用用量投影")
            .WithDescription("从审核请求、审核项和 AI 调用事实重建小时及日用量，不使用估算数据。")
            .Produces<UsageRebuildResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return endpoints;
    }
}
