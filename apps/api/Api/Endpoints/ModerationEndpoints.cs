using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using VeriScan.Api.Authentication;
using VeriScan.Application.Abstractions;
using VeriScan.Application.Contracts;
using VeriScan.Application.Services;

namespace VeriScan.Api.Endpoints;

public static class ModerationEndpoints
{
    public static IEndpointRouteBuilder MapModerationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/moderation")
            .WithTags("Moderation");

        group.MapPost("/batches", async Task<Results<Ok<BatchModerationResponse>, Accepted<BatchModerationResponse>>> (
                BatchModerationRequest request,
                [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
                ClaimsPrincipal user,
                IModerationService service,
                HttpResponse httpResponse,
                CancellationToken cancellationToken) =>
            {
                var principal = user.GetApiKeyPrincipal();
                var response = await service.CreateBatchAsync(
                    request,
                    principal,
                    idempotencyKey,
                    cancellationToken);
                if (response.ProcessingStatus is "accepted" or "processing" or "retry_wait")
                {
                    httpResponse.Headers.RetryAfter = "2";
                    return TypedResults.Accepted(
                        $"/api/v1/moderation/batches/{response.RequestId}",
                        response);
                }

                return TypedResults.Ok(response);
            })
            .RequireAuthorization(ApiKeyAuthenticationDefaults.SubmitPolicy)
            .WithName("CreateModerationBatch")
            .WithSummary("提交批量审核")
            .WithDescription("使用应用 API Key 提交纯文本批量审核，并返回 pass、reject 或 review 终态；建议为可安全重放的请求提供 Idempotency-Key。")
            .Produces<BatchModerationResponse>(StatusCodes.Status200OK)
            .Produces<BatchModerationResponse>(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status504GatewayTimeout);

        group.MapPost("/batches/{requestId:guid}/cancel", async (
                Guid requestId,
                [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
                ClaimsPrincipal user,
                IModerationService service,
                HttpRequest httpRequest,
                CancellationToken cancellationToken) =>
            {
                if (!httpRequest.Headers.TryGetValue("Idempotency-Key", out var idempotencyKeys) ||
                    idempotencyKeys.Count != 1)
                {
                    throw new RequestValidationException(
                        "取消审核批次必须提供且只能提供一个 Idempotency-Key。");
                }

                var principal = user.GetApiKeyPrincipal();
                var response = await service.CancelBatchAsync(
                    requestId,
                    principal,
                    idempotencyKey,
                    cancellationToken);
                return TypedResults.Ok(response);
            })
            .RequireAuthorization(ApiKeyAuthenticationDefaults.SubmitPolicy)
            .WithName("CancelModerationBatch")
            .WithSummary("取消尚未开始的异步审核批次")
            .WithDescription("必须提供唯一的 Idempotency-Key；只取消仍处于 accepted 或 retry_wait 状态的项目，同键重放返回首次响应，已经开始或终结的批次返回状态冲突。")
            .Produces<BatchModerationResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

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
