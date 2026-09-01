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
        var resolver = new ExternalAiCredentialResolver(configuration);

        Assert.True(resolver.TryResolve("config://ProviderA", out var credential));
        Assert.Equal("provider-secret", credential);
        Assert.False(resolver.TryResolve("provider-secret", out _));
        Assert.False(resolver.TryResolve("config://missing", out _));
        Assert.False(resolver.TryResolve("config://../ProviderA", out _));
    }

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
