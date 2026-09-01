using VeriScan.Api.Authentication;
using VeriScan.Application.Contracts;
using VeriScan.Application.Services;

namespace VeriScan.Api.Endpoints;

public static class RuleSetEndpoints
{
    public static IEndpointRouteBuilder MapRuleSetEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/admin/v1/rule-sets")
            .WithTags("Rule sets")
            .RequireAuthorization(AdminJwtOptions.Policy, AdminPolicies.Viewer);

        group.MapGet("", async (
                IRuleSetService service,
                CancellationToken cancellationToken) =>
                TypedResults.Ok(await service.ListAsync(cancellationToken)))
            .WithName("ListRuleSets")
            .WithSummary("查询规则集版本")
            .Produces<RuleSetListResponse>(StatusCodes.Status200OK);

        group.MapGet("/{ruleSetId:guid}", async (
                Guid ruleSetId,
                IRuleSetService service,
                CancellationToken cancellationToken) =>
                TypedResults.Ok(await service.GetAsync(ruleSetId, cancellationToken)))
            .WithName("GetRuleSet")
            .WithSummary("查询规则集版本详情")
            .Produces<RuleSetResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("", async (
                CreateRuleSetRequest request,
                IRuleSetService service,
                CancellationToken cancellationToken) =>
            {
                var response = await service.CreateAsync(request, cancellationToken);
                return TypedResults.Created($"/api/admin/v1/rule-sets/{response.Id}", response);
            })
            .RequireAuthorization(AdminPolicies.RuleEditor)
            .WithName("CreateRuleSet")
            .WithSummary("创建规则集草稿")
            .Produces<RuleSetResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapPut("/{ruleSetId:guid}", async (
                Guid ruleSetId,
                RuleSetDraftRequest request,
                IRuleSetService service,
                CancellationToken cancellationToken) =>
                TypedResults.Ok(await service.UpdateAsync(ruleSetId, request, cancellationToken)))
            .RequireAuthorization(AdminPolicies.RuleEditor)
            .WithName("UpdateRuleSet")
            .WithSummary("更新规则集草稿")
            .Produces<RuleSetResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{ruleSetId:guid}/revisions", async (
                Guid ruleSetId,
                IRuleSetService service,
                CancellationToken cancellationToken) =>
            {
                var response = await service.CreateRevisionAsync(ruleSetId, cancellationToken);
                return TypedResults.Created($"/api/admin/v1/rule-sets/{response.Id}", response);
            })
            .RequireAuthorization(AdminPolicies.RuleEditor)
            .WithName("CreateRuleSetRevision")
            .WithSummary("基于现有版本创建新草稿")
            .Produces<RuleSetResponse>(StatusCodes.Status201Created);

        group.MapPost("/{ruleSetId:guid}/validate", async (
                Guid ruleSetId,
                IRuleSetService service,
                CancellationToken cancellationToken) =>
                TypedResults.Ok(await service.ValidateAsync(ruleSetId, cancellationToken)))
            .RequireAuthorization(AdminPolicies.RuleEditor)
            .WithName("ValidateRuleSet")
            .WithSummary("校验规则集草稿")
            .Produces<RuleSetValidationResponse>(StatusCodes.Status200OK);

        group.MapPost("/{ruleSetId:guid}/publish", async (
                Guid ruleSetId,
                IRuleSetService service,
                CancellationToken cancellationToken) =>
                TypedResults.Ok(await service.PublishAsync(ruleSetId, cancellationToken)))
            .RequireAuthorization(AdminPolicies.Publisher)
            .WithName("PublishRuleSet")
            .WithSummary("发布不可变规则集版本")
            .Produces<RuleSetResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{ruleSetId:guid}/archive", async (
                Guid ruleSetId,
                IRuleSetService service,
                CancellationToken cancellationToken) =>
                TypedResults.Ok(await service.ArchiveAsync(ruleSetId, cancellationToken)))
            .RequireAuthorization(AdminPolicies.Publisher)
            .WithName("ArchiveRuleSet")
            .WithSummary("归档未被应用绑定的规则集版本")
            .Produces<RuleSetResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return endpoints;
    }
}
