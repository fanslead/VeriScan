using System.Security.Claims;
using VeriScan.Api.Authentication;
using VeriScan.Application.Contracts;
using VeriScan.Application.Services;

namespace VeriScan.Api.Endpoints;

public static class ModerationEndpoints
{
    public static IEndpointRouteBuilder MapModerationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/moderation")
            .WithTags("Moderation");

        group.MapPost("/batches", async (
                BatchModerationRequest request,
                ClaimsPrincipal user,
                IModerationService service,
                CancellationToken cancellationToken) =>
            {
                var principal = user.GetApiKeyPrincipal();
                var response = await service.CreateBatchAsync(request, principal, cancellationToken);
                return TypedResults.Ok(response);
            })
            .RequireAuthorization(ApiKeyAuthenticationDefaults.SubmitPolicy)
            .WithName("CreateModerationBatch")
            .WithSummary("提交批量审核")
            .WithDescription("使用应用 API Key 提交纯文本批量审核，并返回 pass、reject 或 review 终态。")
            .Produces<BatchModerationResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapGet("/batches/{requestId:guid}", async (
                Guid requestId,
                ClaimsPrincipal user,
                IModerationService service,
                CancellationToken cancellationToken) =>
            {
                var principal = user.GetApiKeyPrincipal();
                var response = await service.GetBatchAsync(requestId, principal, cancellationToken);
                return TypedResults.Ok(response);
            })
            .RequireAuthorization(ApiKeyAuthenticationDefaults.ReadPolicy)
            .WithName("GetModerationBatch")
            .WithSummary("查询审核批次")
            .WithDescription("只允许查询当前 API Key 所属应用的审核记录。")
            .Produces<BatchModerationResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return endpoints;
    }
}
