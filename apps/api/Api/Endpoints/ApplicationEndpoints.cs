using VeriScan.Api.Authentication;
using VeriScan.Application.Contracts;
using VeriScan.Application.Services;

namespace VeriScan.Api.Endpoints;

public static class ApplicationEndpoints
{
    public static IEndpointRouteBuilder MapApplicationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/admin/v1/applications")
            .WithTags("Applications")
            .RequireAuthorization(AdminJwtOptions.Policy, AdminPolicies.Viewer);

        group.MapPost("", async (
                CreateApplicationRequest request,
                IApplicationService service,
                CancellationToken cancellationToken) =>
            {
                var response = await service.CreateAsync(request, cancellationToken);
                return TypedResults.Created($"/api/admin/v1/applications/{response.Id}", response);
            })
            .RequireAuthorization(AdminPolicies.Operator)
            .WithName("CreateApplication")
            .WithSummary("创建应用")
            .WithDescription("创建一个用于审核调用、配额和统计归属的应用。")
            .Produces<ApplicationResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapGet("", async (
                IApplicationService service,
                CancellationToken cancellationToken) =>
            {
                var response = await service.ListAsync(cancellationToken);
                return TypedResults.Ok(response);
            })
            .WithName("ListApplications")
            .WithSummary("查询应用列表")
            .WithDescription("返回当前管理边界内的应用及活跃 Key 数量。")
            .Produces<ApplicationListResponse>(StatusCodes.Status200OK);

        group.MapGet("/{applicationId:guid}", async (
                Guid applicationId,
                IApplicationService service,
                CancellationToken cancellationToken) =>
            {
                var response = await service.GetAsync(applicationId, cancellationToken);
                return TypedResults.Ok(response);
            })
            .WithName("GetApplication")
            .WithSummary("查询应用详情")
            .WithDescription("返回应用状态、环境和活跃 API Key 数量。")
            .Produces<ApplicationResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPatch("/{applicationId:guid}", async (
                Guid applicationId,
                UpdateApplicationRequest request,
                IApplicationService service,
                CancellationToken cancellationToken) =>
            {
                var response = await service.UpdateAsync(applicationId, request, cancellationToken);
                return TypedResults.Ok(response);
            })
            .RequireAuthorization(AdminPolicies.Operator)
            .WithName("UpdateApplication")
            .WithSummary("更新应用")
            .WithDescription("更新应用名称或生命周期状态。")
            .Produces<ApplicationResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/{applicationId:guid}/rule-set", async (
                Guid applicationId,
                BindApplicationRuleSetRequest request,
                IApplicationService service,
                CancellationToken cancellationToken) =>
            {
                var response = await service.BindRuleSetAsync(applicationId, request, cancellationToken);
                return TypedResults.Ok(response);
            })
            .RequireAuthorization(AdminPolicies.RuleEditor)
            .WithName("BindApplicationRuleSet")
            .WithSummary("切换应用规则集版本")
            .WithDescription("只能绑定已发布版本；切换后新请求立即使用新版本，历史请求保留原版本。")
            .Produces<ApplicationResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return endpoints;
    }
}
