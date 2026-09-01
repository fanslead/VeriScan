using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using VeriScan.Application.Abstractions;
using VeriScan.Infrastructure.ExternalAi;

namespace VeriScan.Api.Tests;

public sealed class ExternalAiPolicyTests
{
    [Fact]
    public void EndpointPolicyRequiresHttpsAllowlistedHostAndApprovedPort()
    {
        var policy = new ExternalAiEndpointPolicy(new StaticOptionsMonitor<ExternalAiOptions>(new ExternalAiOptions
        {
            AllowedHosts = ["api.example.com", "*.approved.example"],
            AllowedPorts = [443, 8443]
        }));

        policy.Validate(new Uri("https://api.example.com/v1/responses"));
        policy.Validate(new Uri("https://region.approved.example:8443/v1/messages"));

        Assert.Throws<RequestValidationException>(() => policy.Validate(new Uri("http://api.example.com/v1/responses")));
        Assert.Throws<RequestValidationException>(() => policy.Validate(new Uri("https://not-approved.example/v1/responses")));
        Assert.Throws<RequestValidationException>(() => policy.Validate(new Uri("https://api.example.com:9443/v1/responses")));
        Assert.Throws<RequestValidationException>(() => policy.Validate(new Uri("https://user:secret@api.example.com/v1/responses")));
        Assert.Throws<RequestValidationException>(() => policy.Validate(new Uri("https://127.0.0.1/v1/responses")));
        Assert.Throws<RequestValidationException>(() => policy.Validate(new Uri("https://localhost/v1/responses")));
        Assert.Throws<RequestValidationException>(() => policy.Validate(new Uri("https://api.example.com/v1/responses?key=secret")));
    }

    [Fact]
    public void CredentialResolverOnlyReadsConfigReferenceAndNeverTreatsReferenceAsSecret()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ExternalAi:Credentials:ProviderA"] = "provider-secret"
            })
            .Build();
        var protector = CreateProtector();
        var resolver = new ExternalAiCredentialResolver(configuration, protector);
        var configured = CreateConfiguration("config://ProviderA");
        var missing = CreateConfiguration("config://missing");
        var plaintext = CreateConfiguration("provider-secret");
        var traversal = CreateConfiguration("config://../ProviderA");

        Assert.True(resolver.TryResolve(configured, out var credential));
        Assert.Equal("provider-secret", credential);
        Assert.False(resolver.TryResolve(plaintext, out _));
        Assert.False(resolver.TryResolve(missing, out _));
        Assert.False(resolver.TryResolve(traversal, out _));
    }

    [Fact]
    public void ManagedCredentialIsEncryptedAndTamperingIsRejected()
    {
        var protector = CreateProtector();
        const string secret = "sk-managed-provider-secret";

        var protectedValue = protector.Protect(secret);

        Assert.DoesNotContain(secret, protectedValue, StringComparison.Ordinal);
        Assert.True(protector.TryUnprotect(protectedValue, out var restored));
        Assert.Equal(secret, restored);
        Assert.False(protector.TryUnprotect(protectedValue + "tampered", out _));
    }

    private static AiCredentialProtector CreateProtector() => new(Options.Create(
        new AiCredentialEncryptionOptions
        {
            MasterKey = "dmVyaXNjYW4tdGVzdC1tYXN0ZXIta2V5LTMyLWJ5dGU="
        }));

    private static VeriScan.Domain.Entities.AiModelConfiguration CreateConfiguration(string credentialRef) => new(
        "测试配置",
        VeriScan.Domain.Entities.AiProtocol.OpenAiResponses,
        "https://api.example.com",
        "/v1/responses",
        credentialRef,
        VeriScan.Domain.Entities.AiAuthScheme.Bearer,
        "model",
        null,
        VeriScan.Domain.Entities.AiApiVersionLocation.None,
        "你是内容审核助手。请仅返回规定格式的结构化审核结果。",
        VeriScan.Domain.Entities.AiDecodingMode.OmitTemperature,
        4096,
        512,
        2000,
        15000,
        2,
        "global",
        "30d");

    private sealed class StaticOptionsMonitor<T>(T currentValue) : IOptionsMonitor<T>
    {
        public T CurrentValue => currentValue;

        public T Get(string? name) => currentValue;

        public IDisposable OnChange(Action<T, string?> listener) => NoopDisposable.Instance;

        private sealed class NoopDisposable : IDisposable
        {
            public static NoopDisposable Instance { get; } = new();

            public void Dispose()
            {
            }
        }
    }
}
