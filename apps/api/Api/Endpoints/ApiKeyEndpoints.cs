using VeriScan.Api.Authentication;
using VeriScan.Application.Contracts;
using VeriScan.Application.Services;

namespace VeriScan.Api.Endpoints;

public static class ApiKeyEndpoints
{
    public static IEndpointRouteBuilder MapApiKeyEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/admin/v1/applications/{applicationId:guid}/api-keys")
            .WithTags("Application API Keys")
            .RequireAuthorization(AdminJwtOptions.Policy);

        group.MapPost("", async (
                Guid applicationId,
                CreateApiKeyRequest request,
                IApiKeyService service,
                CancellationToken cancellationToken) =>
            {
                var response = await service.CreateAsync(applicationId, request, cancellationToken);
                return TypedResults.Created(
                    $"/api/admin/v1/applications/{applicationId}/api-keys/{response.KeyId}",
                    response);
            })
            .WithName("CreateApplicationApiKey")
            .WithSummary("创建应用 API Key")
            .WithDescription("创建成功时返回一次完整明文 Key，之后只能查看脱敏摘要。")
            .Produces<ApiKeyCreatedResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapGet("", async (
                Guid applicationId,
                IApiKeyService service,
                CancellationToken cancellationToken) =>
            {
                var response = await service.ListAsync(applicationId, cancellationToken);
                return TypedResults.Ok(response);
            })
            .WithName("ListApplicationApiKeys")
            .WithSummary("查询应用 API Key")
            .WithDescription("返回 Key 的脱敏摘要，不返回任何完整明文。")
            .Produces<ApiKeyListResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{keyId:guid}/rotate", async (
                Guid applicationId,
                Guid keyId,
                RotateApiKeyRequest request,
                IApiKeyService service,
                CancellationToken cancellationToken) =>
            {
                var response = await service.RotateAsync(applicationId, keyId, request, cancellationToken);
                return TypedResults.Created(
                    $"/api/admin/v1/applications/{applicationId}/api-keys/{response.KeyId}",
                    response);
            })
            .WithName("RotateApplicationApiKey")
            .WithSummary("轮换应用 API Key")
            .WithDescription("先创建新 Key；可按请求选择是否立即撤销旧 Key。")
            .Produces<ApiKeyCreatedResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapDelete("/{keyId:guid}", async (
                Guid applicationId,
                Guid keyId,
                IApiKeyService service,
                CancellationToken cancellationToken) =>
            {
                await service.RevokeAsync(applicationId, keyId, cancellationToken);
                return TypedResults.NoContent();
            })
            .WithName("RevokeApplicationApiKey")
            .WithSummary("撤销应用 API Key")
            .WithDescription("撤销后该 Key 不能再通过业务审核 API 认证。")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return endpoints;
    }
}
