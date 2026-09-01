using System.Text.RegularExpressions;
using VeriScan.Application.Abstractions;
using VeriScan.Application.Contracts;
using VeriScan.Domain.Entities;

namespace VeriScan.Application.Services;

public sealed partial class AiConfigurationService
{
    private ValidatedDraft Validate(
        AiConfigurationDraftRequest request,
        AiModelConfiguration? current = null)
    {
        var name = request.Name.Trim();
        var baseUrl = request.BaseUrl.Trim();
        var endpointPath = request.EndpointPath.Trim();
        var credentialRef = request.CredentialRef?.Trim();
        var credentialSecret = request.ApiKey?.Trim();
        var model = request.Model.Trim();
        var systemPrompt = request.SystemPrompt.Trim();
        var dataRegion = request.DataRegion.Trim();
        var retentionClass = request.RetentionClass.Trim();

        if (name.Length < 2 || model.Length == 0 || systemPrompt.Length < 20)
        {
            throw new RequestValidationException("AI 配置名称、模型和系统提示词不能为空。");
        }

        if (!endpointPath.StartsWith('/') ||
            endpointPath.Contains('?', StringComparison.Ordinal) ||
            endpointPath.Contains('#', StringComparison.Ordinal) ||
            endpointPath.StartsWith("//", StringComparison.Ordinal))
        {
            throw new RequestValidationException("AI 端点路径必须是无查询参数的站内绝对路径。");
        }

        if (!string.IsNullOrWhiteSpace(credentialRef) && !CredentialReferencePattern().IsMatch(credentialRef))
        {
            throw new RequestValidationException("credentialRef 必须使用 config://名称 格式。");
        }

        var hasCurrentCredential = current is not null &&
            (!string.IsNullOrWhiteSpace(current.CredentialCiphertext) ||
             CredentialReferencePattern().IsMatch(current.CredentialRef));
        if (string.IsNullOrWhiteSpace(credentialSecret) &&
            string.IsNullOrWhiteSpace(credentialRef) &&
            !hasCurrentCredential)
        {
            throw new RequestValidationException("请在管理后台填写 AI API 密钥。");
        }

        if (request.RequestTimeoutMs <= request.ConnectTimeoutMs)
        {
            throw new RequestValidationException("请求超时必须大于连接超时。");
        }

        var hasApiVersion = !string.IsNullOrWhiteSpace(request.ApiVersion);
        var hasApiVersionLocation = request.ApiVersionLocation != AiApiVersionLocation.None;
        if (hasApiVersion != hasApiVersionLocation)
        {
            throw new RequestValidationException("apiVersion 与 apiVersionLocation 必须同时配置或同时省略。");
        }

        if (request.Protocol == AiProtocol.AnthropicMessages &&
            (!hasApiVersion || request.ApiVersionLocation != AiApiVersionLocation.Header))
        {
            throw new RequestValidationException("Messages 协议必须通过受控 Header 显式配置 apiVersion。");
        }

        if (request.Protocol != AiProtocol.AnthropicMessages &&
            request.ApiVersionLocation == AiApiVersionLocation.Header)
        {
            throw new RequestValidationException("OpenAI 协议的 apiVersion 仅支持固定 api-version 查询参数。");
        }

        if (string.IsNullOrWhiteSpace(dataRegion) || string.IsNullOrWhiteSpace(retentionClass))
        {
            throw new RequestValidationException("数据地域和保留策略必须显式配置。");
        }

        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri) ||
            baseUri.AbsolutePath != "/" ||
            !string.IsNullOrEmpty(baseUri.Query) ||
            !string.IsNullOrEmpty(baseUri.Fragment))
        {
            throw new RequestValidationException("baseUrl 必须是仅包含 scheme、host 和可选端口的绝对地址。");
        }

        endpointPolicy.Validate(new Uri(baseUri, endpointPath));
        return new ValidatedDraft(
            name,
            request.Protocol,
            baseUri.GetLeftPart(UriPartial.Authority),
            endpointPath,
            string.IsNullOrWhiteSpace(credentialRef) ? current?.CredentialRef : credentialRef,
            string.IsNullOrWhiteSpace(credentialSecret) ? null : credentialSecret,
            !string.IsNullOrWhiteSpace(credentialRef),
            request.AuthScheme,
            model,
            string.IsNullOrWhiteSpace(request.ApiVersion) ? null : request.ApiVersion.Trim(),
            request.ApiVersionLocation,
            systemPrompt,
            request.DecodingMode,
            request.MaxInputTokens,
            request.MaxOutputTokens,
            request.ConnectTimeoutMs,
            request.RequestTimeoutMs,
            request.MaxAttempts,
            dataRegion,
            retentionClass);
    }

    private void ValidateEndpoint(AiModelConfiguration configuration)
    {
        endpointPolicy.Validate(new Uri(new Uri(configuration.BaseUrl), configuration.EndpointPath));
    }

    [GeneratedRegex("^config://[A-Za-z][A-Za-z0-9_.-]{0,127}$", RegexOptions.CultureInvariant)]
    private static partial Regex CredentialReferencePattern();

    private sealed record ValidatedDraft(
        string Name,
        AiProtocol Protocol,
        string BaseUrl,
        string EndpointPath,
        string? CredentialRef,
        string? CredentialSecret,
        bool ReplacesCredentialReference,
        AiAuthScheme AuthScheme,
        string Model,
        string? ApiVersion,
        AiApiVersionLocation ApiVersionLocation,
        string SystemPrompt,
        AiDecodingMode DecodingMode,
        int MaxInputTokens,
        int MaxOutputTokens,
        int ConnectTimeoutMs,
        int RequestTimeoutMs,
        int MaxAttempts,
        string DataRegion,
        string RetentionClass);

    private static AiModelConfiguration CreateEntity(ValidatedDraft draft)
    {
        return new AiModelConfiguration(
            draft.Name,
            draft.Protocol,
            draft.BaseUrl,
            draft.EndpointPath,
            draft.CredentialRef ?? "managed://encrypted",
            draft.AuthScheme,
            draft.Model,
            draft.ApiVersion,
            draft.ApiVersionLocation,
            draft.SystemPrompt,
            draft.DecodingMode,
            draft.MaxInputTokens,
            draft.MaxOutputTokens,
            draft.ConnectTimeoutMs,
            draft.RequestTimeoutMs,
            draft.MaxAttempts,
            draft.DataRegion,
            draft.RetentionClass);
    }

    private static void ApplyDraft(AiModelConfiguration configuration, ValidatedDraft draft)
    {
        configuration.UpdateDraft(
            draft.Name,
            draft.Protocol,
            draft.BaseUrl,
            draft.EndpointPath,
            draft.AuthScheme,
            draft.Model,
            draft.ApiVersion,
            draft.ApiVersionLocation,
            draft.SystemPrompt,
            draft.DecodingMode,
            draft.MaxInputTokens,
            draft.MaxOutputTokens,
            draft.ConnectTimeoutMs,
            draft.RequestTimeoutMs,
            draft.MaxAttempts,
            draft.DataRegion,
            draft.RetentionClass);
    }

    private void ApplyCredential(AiModelConfiguration configuration, ValidatedDraft draft)
    {
        if (!string.IsNullOrWhiteSpace(draft.CredentialSecret))
        {
            configuration.SetManagedCredential(credentialProtector.Protect(draft.CredentialSecret));
        }
        else if (draft.ReplacesCredentialReference && !string.IsNullOrWhiteSpace(draft.CredentialRef))
        {
            configuration.UseExternalCredentialReference(draft.CredentialRef);
        }
    }
}
