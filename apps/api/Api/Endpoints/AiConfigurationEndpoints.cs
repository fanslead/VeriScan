using VeriScan.Api.Authentication;
using VeriScan.Application.Contracts;
using VeriScan.Application.Services;

namespace VeriScan.Api.Endpoints;

public static class AiConfigurationEndpoints
{
    public static IEndpointRouteBuilder MapAiConfigurationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/admin/v1/ai/configurations")
            .WithTags("AI configurations")
            .RequireAuthorization(AdminJwtOptions.Policy);

        group.MapGet("", ListAsync)
            .WithName("ListAiConfigurations")
            .WithSummary("查询 AI 模型配置")
            .Produces<AiConfigurationListResponse>(StatusCodes.Status200OK);

        group.MapGet("/{configurationId:guid}", GetAsync)
            .WithName("GetAiConfiguration")
            .WithSummary("查询 AI 模型配置详情")
            .Produces<AiConfigurationResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("", CreateAsync)
            .WithName("CreateAiConfiguration")
            .WithSummary("创建 AI 配置草稿")
            .WithDescription("凭证明文必须由服务端密钥配置提供；此接口只保存 config:// 引用。")
            .Produces<AiConfigurationResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapPut("/{configurationId:guid}", UpdateAsync)
            .WithName("UpdateAiConfiguration")
            .WithSummary("更新 AI 配置草稿")
            .Produces<AiConfigurationResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{configurationId:guid}/revisions", CreateRevisionAsync)
            .WithName("CreateAiConfigurationRevision")
            .WithSummary("基于现有版本创建新草稿")
            .Produces<AiConfigurationResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{configurationId:guid}/test", TestAsync)
            .WithName("TestAiConfiguration")
            .WithSummary("使用合成文本测试 AI 配置")
            .Produces<AiConfigurationTestResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{configurationId:guid}/publish", PublishAsync)
            .WithName("PublishAiConfiguration")
            .WithSummary("发布不可变 AI 配置")
            .Produces<AiConfigurationResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{configurationId:guid}/activate", ActivateAsync)
            .WithName("ActivateAiConfiguration")
            .WithSummary("激活已发布 AI 配置")
            .WithDescription("激活旧版本可执行回滚；同一时刻仅一个配置全局生效。")
            .Produces<AiConfigurationResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{configurationId:guid}/archive", ArchiveAsync)
            .WithName("ArchiveAiConfiguration")
            .WithSummary("归档 AI 配置")
            .Produces<AiConfigurationResponse>(StatusCodes.Status200OK);

        return endpoints;
    }

    private static async Task<IResult> ListAsync(
        IAiConfigurationService service,
        CancellationToken cancellationToken)
    {
        return TypedResults.Ok(await service.ListAsync(cancellationToken));
    }

    private static async Task<IResult> GetAsync(
        Guid configurationId,
        IAiConfigurationService service,
        CancellationToken cancellationToken)
    {
        return TypedResults.Ok(await service.GetAsync(configurationId, cancellationToken));
    }

    private static async Task<IResult> CreateAsync(
        CreateAiConfigurationRequest request,
        IAiConfigurationService service,
        CancellationToken cancellationToken)
    {
        var response = await service.CreateAsync(request, cancellationToken);
        return TypedResults.Created($"/api/admin/v1/ai/configurations/{response.Id}", response);
    }

    private static async Task<IResult> UpdateAsync(
        Guid configurationId,
        AiConfigurationDraftRequest request,
        IAiConfigurationService service,
        CancellationToken cancellationToken)
    {
        return TypedResults.Ok(await service.UpdateAsync(configurationId, request, cancellationToken));
    }

    private static async Task<IResult> TestAsync(
        Guid configurationId,
        IAiConfigurationService service,
        CancellationToken cancellationToken)
    {
        return TypedResults.Ok(await service.TestAsync(configurationId, cancellationToken));
    }

    private static async Task<IResult> CreateRevisionAsync(
        Guid configurationId,
        IAiConfigurationService service,
        CancellationToken cancellationToken)
    {
        var response = await service.CreateRevisionAsync(configurationId, cancellationToken);
        return TypedResults.Created($"/api/admin/v1/ai/configurations/{response.Id}", response);
    }

    private static async Task<IResult> PublishAsync(
        Guid configurationId,
        IAiConfigurationService service,
        CancellationToken cancellationToken)
    {
        return TypedResults.Ok(await service.PublishAsync(configurationId, cancellationToken));
    }

    private static async Task<IResult> ActivateAsync(
        Guid configurationId,
        IAiConfigurationService service,
        CancellationToken cancellationToken)
    {
        return TypedResults.Ok(await service.ActivateAsync(configurationId, cancellationToken));
    }

    private static async Task<IResult> ArchiveAsync(
        Guid configurationId,
        IAiConfigurationService service,
        CancellationToken cancellationToken)
    {
        return TypedResults.Ok(await service.ArchiveAsync(configurationId, cancellationToken));
    }
}
