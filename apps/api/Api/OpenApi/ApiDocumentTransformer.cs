using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace VeriScan.Api.OpenApi;

public sealed class ApiDocumentTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        var components = document.Components ??= new OpenApiComponents();
        var securitySchemes = components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        securitySchemes["ApiKey"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.ApiKey,
            In = ParameterLocation.Header,
            Name = "X-API-Key",
            Description = "服务端调用使用的应用 API Key。"
        };
        securitySchemes["Bearer"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "管理端使用的 OIDC Bearer 访问令牌。"
        };

        if (document.Paths is null)
        {
            return Task.CompletedTask;
        }

        foreach (var path in document.Paths)
        {
            var schemeName = path.Key.StartsWith("/api/admin/v1/", StringComparison.Ordinal)
                ? "Bearer"
                : path.Key.StartsWith("/api/v1/", StringComparison.Ordinal)
                    ? "ApiKey"
                    : null;
            if (schemeName is null)
            {
                continue;
            }

            var schemeReference = new OpenApiSecuritySchemeReference(
                schemeName,
                document,
                null);
            if (path.Value is not OpenApiPathItem pathItem)
            {
                continue;
            }

            var operations = pathItem.Operations;
            if (operations is null)
            {
                continue;
            }

            foreach (var operation in operations.Values)
            {
                operation.Security =
                [
                    new OpenApiSecurityRequirement
                    {
                        [schemeReference] = []
                    }
                ];
            }
        }

        return Task.CompletedTask;
    }
}
