using VeriScan.Api.Authentication;
using VeriScan.Application.Contracts;
using VeriScan.Application.Services;

namespace VeriScan.Api.Endpoints;

/// <summary>管理端审计事件查询接口。</summary>
public static class AuditEventEndpoints
{
    public static IEndpointRouteBuilder MapAuditEventEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/admin/v1/audit-events", async (
                [AsParameters] AuditEventQuery query,
                IAuditQueryService service,
                CancellationToken cancellationToken) =>
            {
                var response = await service.ListAsync(query, cancellationToken);
                return TypedResults.Ok(response);
            })
            .RequireAuthorization(AdminJwtOptions.Policy, AdminPolicies.Auditor)
            .WithTags("Audit")
            .WithName("ListAuditEvents")
            .WithSummary("查询审计事件")
            .WithDescription("查询管理操作安全摘要，不返回原文、完整 API Key 或凭证。")
            .Produces<AuditEventListResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);
        return endpoints;
    }
}
