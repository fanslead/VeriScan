using VeriScan.Api.Authentication;
using VeriScan.Application.Contracts;
using VeriScan.Application.Services;

namespace VeriScan.Api.Endpoints;

/// <summary>应用 Webhook 配置、连接测试与签名密钥管理接口。</summary>
public static class ApplicationWebhookEndpoints
{
    public static IEndpointRouteBuilder MapApplicationWebhookEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/admin/v1/applications/{applicationId:guid}/webhook")
            .WithTags("Application Webhook")
            .RequireAuthorization(AdminJwtOptions.Policy, AdminPolicies.Viewer);

        group.MapGet("", async (
                Guid applicationId,
                IApplicationWebhookService service,
                CancellationToken cancellationToken) =>
            {
                var response = await service.GetAsync(applicationId, cancellationToken);
                return TypedResults.Ok(response);
            })
            .WithName("GetApplicationWebhook")
            .WithSummary("查询应用 Webhook 配置")
            .Produces<ApplicationWebhookResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("", async (
                Guid applicationId,
                SaveApplicationWebhookRequest request,
                IApplicationWebhookService service,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                var response = await service.SaveAsync(applicationId, request, cancellationToken);
                httpContext.Response.Headers.CacheControl = "no-store";
                return TypedResults.Ok(response);
            })
            .RequireAuthorization(AdminPolicies.Operator)
            .WithName("SaveApplicationWebhook")
            .WithSummary("保存应用 Webhook 地址")
            .WithDescription("保存后需完成连接测试，才能启用异步审核结果通知。")
            .Produces<ApplicationWebhookSavedResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        group.MapPatch("", async (
                Guid applicationId,
                SetApplicationWebhookStatusRequest request,
                IApplicationWebhookService service,
                CancellationToken cancellationToken) =>
            {
                var response = await service.SetStatusAsync(
                    applicationId,
                    request,
                    cancellationToken);
                return TypedResults.Ok(response);
            })
            .RequireAuthorization(AdminPolicies.Operator)
            .WithName("SetApplicationWebhookStatus")
            .WithSummary("启用或停用应用 Webhook")
            .Produces<ApplicationWebhookResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/tests", async (
                Guid applicationId,
                IApplicationWebhookService service,
                CancellationToken cancellationToken) =>
            {
                var response = await service.TestAsync(applicationId, cancellationToken);
                return TypedResults.Accepted(response.StatusUrl, response);
            })
            .RequireAuthorization(AdminPolicies.Operator)
            .WithName("TestApplicationWebhook")
            .WithSummary("发送 Webhook 连接测试")
            .Produces<ApplicationWebhookTestAcceptedResponse>(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/tests/{testId:guid}", async (
                Guid applicationId,
                Guid testId,
                IApplicationWebhookService service,
                CancellationToken cancellationToken) =>
            {
                var response = await service.GetTestAsync(
                    applicationId,
                    testId,
                    cancellationToken);
                return TypedResults.Ok(response);
            })
            .WithName("GetApplicationWebhookTest")
            .WithSummary("查询 Webhook 连接测试结果")
            .Produces<ApplicationWebhookTestResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/secret/rotate", async (
                Guid applicationId,
                IApplicationWebhookService service,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                var response = await service.RotateSecretAsync(applicationId, cancellationToken);
                httpContext.Response.Headers.CacheControl = "no-store";
                return TypedResults.Ok(response);
            })
            .RequireAuthorization(AdminPolicies.Operator)
            .WithName("RotateApplicationWebhookSecret")
            .WithSummary("轮换 Webhook 签名密钥")
            .WithDescription("新签名密钥仅在本次响应中返回；轮换后需重新测试并启用通知。")
            .Produces<ApplicationWebhookSecretResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        return endpoints;
    }
}
