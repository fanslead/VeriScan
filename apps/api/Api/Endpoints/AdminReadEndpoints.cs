using VeriScan.Api.Authentication;
using VeriScan.Application.Contracts;
using VeriScan.Application.Services;

namespace VeriScan.Api.Endpoints;

public static class AdminReadEndpoints
{
    public static IEndpointRouteBuilder MapAdminReadEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/admin/v1")
            .WithTags("Admin Read Models")
            .RequireAuthorization(AdminJwtOptions.Policy, AdminPolicies.Viewer);

        group.MapGet("/overview", async (
                IAdminReadService service,
                CancellationToken cancellationToken) =>
            {
                var response = await service.GetOverviewAsync(cancellationToken);
                return TypedResults.Ok(response);
            })
            .WithName("GetAdminOverview")
            .WithSummary("查询管理端概览")
            .WithDescription("返回当前数据库中可重建的当日审核事实和最近记录。")
            .Produces<AdminOverviewResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/moderation-records", async (
                [AsParameters] AdminModerationRecordQuery query,
                IAdminReadService service,
                CancellationToken cancellationToken) =>
            {
                var response = await service.ListRecordsAsync(query, cancellationToken);
                return TypedResults.Ok(response);
            })
            .WithName("ListAdminModerationRecords")
            .WithSummary("分页查询审核记录")
            .WithDescription("按应用、机器决定和关键字查询只读机器审核结果。")
            .Produces<ModerationRecordPageResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/moderation-records/{recordId:guid}", async (
                Guid recordId,
                IAdminReadService service,
                CancellationToken cancellationToken) =>
            {
                var response = await service.GetRecordAsync(recordId, cancellationToken);
                return TypedResults.Ok(response);
            })
            .WithName("GetAdminModerationRecord")
            .WithSummary("查询审核记录详情")
            .WithDescription("返回审核记录的机器结果和存储的事实字段，不生成或写入人工复审决定。")
            .Produces<ModerationRecordResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return endpoints;
    }
}
