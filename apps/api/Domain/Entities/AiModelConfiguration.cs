namespace VeriScan.Domain.Entities;

public sealed class AiModelConfiguration
{
    private AiModelConfiguration()
    {
    }

    public AiModelConfiguration(
        string name,
        AiProtocol protocol,
        string baseUrl,
        string endpointPath,
        string credentialRef,
        AiAuthScheme authScheme,
        string model,
        string? apiVersion,
        AiApiVersionLocation apiVersionLocation,
        string systemPrompt,
        AiDecodingMode decodingMode,
        int maxInputTokens,
        int maxOutputTokens,
        int connectTimeoutMs,
        int requestTimeoutMs,
        int maxAttempts,
        string dataRegion,
        string retentionClass)
    {
        Id = Guid.CreateVersion7();
        PublicRevisionId = $"ai-model@{Id:N}";
        Status = AiConfigurationStatus.Draft;
        CreatedAt = DateTimeOffset.UtcNow;
        ApplyDraft(
            name,
            protocol,
            baseUrl,
            endpointPath,
            credentialRef,
            authScheme,
            model,
            apiVersion,
            apiVersionLocation,
            systemPrompt,
            decodingMode,
            maxInputTokens,
            maxOutputTokens,
            connectTimeoutMs,
            requestTimeoutMs,
            maxAttempts,
            dataRegion,
            retentionClass);
    }

    public Guid Id { get; private set; }

    public string PublicRevisionId { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public AiProtocol Protocol { get; private set; }

    public string BaseUrl { get; private set; } = string.Empty;

    public string EndpointPath { get; private set; } = string.Empty;

    public string CredentialRef { get; private set; } = string.Empty;

    public string? CredentialCiphertext { get; private set; }

    public AiAuthScheme AuthScheme { get; private set; }

    public string Model { get; private set; } = string.Empty;

    public string? ApiVersion { get; private set; }

    public AiApiVersionLocation ApiVersionLocation { get; private set; }

    public string SystemPrompt { get; private set; } = string.Empty;

    public AiDecodingMode DecodingMode { get; private set; }

    public int MaxInputTokens { get; private set; }

    public int MaxOutputTokens { get; private set; }

    public int ConnectTimeoutMs { get; private set; }

    public int RequestTimeoutMs { get; private set; }

    public int MaxAttempts { get; private set; }

    public string DataRegion { get; private set; } = string.Empty;

    public string RetentionClass { get; private set; } = string.Empty;

    public AiConfigurationStatus Status { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public DateTimeOffset? PublishedAt { get; private set; }

    public DateTimeOffset? LastTestedAt { get; private set; }

    public bool? LastTestSucceeded { get; private set; }

    public string? LastTestFailureCode { get; private set; }

    public string? AdapterContractVersion { get; private set; }

    public string? CanonicalSchemaVersion { get; private set; }

    public string? CanonicalSchemaHash { get; private set; }

    public string? EffectiveSchemaHash { get; private set; }

    public string? SchemaTransformerVersion { get; private set; }

    public void UpdateDraft(
        string name,
        AiProtocol protocol,
        string baseUrl,
        string endpointPath,
        AiAuthScheme authScheme,
        string model,
        string? apiVersion,
        AiApiVersionLocation apiVersionLocation,
        string systemPrompt,
        AiDecodingMode decodingMode,
        int maxInputTokens,
        int maxOutputTokens,
        int connectTimeoutMs,
        int requestTimeoutMs,
        int maxAttempts,
        string dataRegion,
        string retentionClass)
    {
        EnsureDraft();
        ApplyDraft(
            name,
            protocol,
            baseUrl,
            endpointPath,
            CredentialRef,
            authScheme,
            model,
            apiVersion,
            apiVersionLocation,
            systemPrompt,
            decodingMode,
            maxInputTokens,
            maxOutputTokens,
            connectTimeoutMs,
            requestTimeoutMs,
            maxAttempts,
            dataRegion,
            retentionClass);
    }

    public void SetManagedCredential(string credentialCiphertext)
    {
        EnsureDraft();
        ArgumentException.ThrowIfNullOrWhiteSpace(credentialCiphertext);
        CredentialRef = "managed://encrypted";
        CredentialCiphertext = credentialCiphertext;
        InvalidateTestResult();
    }

    public void UseExternalCredentialReference(string credentialRef)
    {
        EnsureDraft();
        ArgumentException.ThrowIfNullOrWhiteSpace(credentialRef);
        CredentialRef = credentialRef;
        CredentialCiphertext = null;
        InvalidateTestResult();
    }

    public void CopyCredentialFrom(AiModelConfiguration source)
    {
        EnsureDraft();
        ArgumentNullException.ThrowIfNull(source);
        CredentialRef = source.CredentialRef;
        CredentialCiphertext = source.CredentialCiphertext;
        InvalidateTestResult();
    }

    public void Publish(
        string adapterContractVersion,
        string canonicalSchemaVersion,
        string canonicalSchemaHash,
        string effectiveSchemaHash,
        string schemaTransformerVersion,
        DateTimeOffset publishedAt)
    {
        EnsureDraft();
        AdapterContractVersion = adapterContractVersion;
        CanonicalSchemaVersion = canonicalSchemaVersion;
        CanonicalSchemaHash = canonicalSchemaHash;
        EffectiveSchemaHash = effectiveSchemaHash;
        SchemaTransformerVersion = schemaTransformerVersion;
        Status = AiConfigurationStatus.Published;
        PublishedAt = publishedAt;
        UpdatedAt = publishedAt;
    }

    public void RecordTestResult(bool succeeded, string? failureCode, DateTimeOffset testedAt)
    {
        if (Status == AiConfigurationStatus.Archived)
        {
            throw new InvalidOperationException("已归档的 AI 配置不能记录连接测试。");
        }

        LastTestedAt = testedAt;
        LastTestSucceeded = succeeded;
        LastTestFailureCode = failureCode;
    }

    public void Activate(DateTimeOffset activatedAt)
    {
        if (Status != AiConfigurationStatus.Published)
        {
            throw new InvalidOperationException("只有已发布的 AI 配置才能激活。");
        }

        IsActive = true;
        UpdatedAt = activatedAt;
    }

    public void Deactivate(DateTimeOffset deactivatedAt)
    {
        IsActive = false;
        UpdatedAt = deactivatedAt;
    }

    public void Archive(DateTimeOffset archivedAt)
    {
        IsActive = false;
        Status = AiConfigurationStatus.Archived;
        UpdatedAt = archivedAt;
    }

    private void ApplyDraft(
        string name,
        AiProtocol protocol,
        string baseUrl,
        string endpointPath,
        string credentialRef,
        AiAuthScheme authScheme,
        string model,
        string? apiVersion,
        AiApiVersionLocation apiVersionLocation,
        string systemPrompt,
        AiDecodingMode decodingMode,
        int maxInputTokens,
        int maxOutputTokens,
        int connectTimeoutMs,
        int requestTimeoutMs,
        int maxAttempts,
        string dataRegion,
        string retentionClass)
    {
        Name = name;
        Protocol = protocol;
        BaseUrl = baseUrl;
        EndpointPath = endpointPath;
        CredentialRef = credentialRef;
        AuthScheme = authScheme;
        Model = model;
        ApiVersion = apiVersion;
        ApiVersionLocation = apiVersionLocation;
        SystemPrompt = systemPrompt;
        DecodingMode = decodingMode;
        MaxInputTokens = maxInputTokens;
        MaxOutputTokens = maxOutputTokens;
        ConnectTimeoutMs = connectTimeoutMs;
        RequestTimeoutMs = requestTimeoutMs;
        MaxAttempts = maxAttempts;
        DataRegion = dataRegion;
        RetentionClass = retentionClass;
        InvalidateTestResult();
    }

    private void InvalidateTestResult()
    {
        LastTestedAt = null;
        LastTestSucceeded = null;
        LastTestFailureCode = null;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    private void EnsureDraft()
    {
        if (Status != AiConfigurationStatus.Draft)
        {
            throw new InvalidOperationException("已发布或已归档的 AI 配置不可原地修改。");
        }
    }
}
