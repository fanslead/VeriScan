using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
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
                [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
                ClaimsPrincipal user,
                IModerationService service,
                CancellationToken cancellationToken) =>
            {
                var principal = user.GetApiKeyPrincipal();
                var response = await service.CreateBatchAsync(
                    request,
                    principal,
                    idempotencyKey,
                    cancellationToken);
                return TypedResults.Ok(response);
            })
            .RequireAuthorization(ApiKeyAuthenticationDefaults.SubmitPolicy)
            .WithName("CreateModerationBatch")
            .WithSummary("提交批量审核")
            .WithDescription("使用应用 API Key 提交纯文本批量审核，并返回 pass、reject 或 review 终态；建议为可安全重放的请求提供 Idempotency-Key。")
            .Produces<BatchModerationResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict)
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
