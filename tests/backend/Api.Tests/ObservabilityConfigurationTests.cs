using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using VeriScan.Api.Diagnostics;

namespace VeriScan.Api.Tests;

public sealed class ObservabilityConfigurationTests
{
    [Fact]
    public void OtlpExporterIsDisabledByDefault()
    {
        var configuration = new ConfigurationBuilder().Build();

        var services = new ServiceCollection();
        services.AddVeriScanObservability(configuration, "VeriScan.Api");

        var options = services
            .BuildServiceProvider()
            .GetRequiredService<Microsoft.Extensions.Options.IOptions<ObservabilityOptions>>()
            .Value;

        Assert.False(options.Otlp.Enabled);
    }

    [Theory]
    [InlineData("https://collector.example/otlp?token=secret")]
    [InlineData("https://user:password@collector.example/otlp")]
    [InlineData("file:///tmp/telemetry")]
    public void OtlpEndpointRejectsUnsafeValues(string endpoint)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Observability:Otlp:Enabled"] = "true",
                ["Observability:Otlp:Endpoint"] = endpoint
            })
            .Build();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            new ServiceCollection().AddVeriScanObservability(configuration, "VeriScan.Api"));

        Assert.Contains("Endpoint", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void OtlpProtocolSupportsHttpProtobuf()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Observability:Otlp:Enabled"] = "true",
                ["Observability:Otlp:Endpoint"] = "https://collector.example/otlp",
                ["Observability:Otlp:Protocol"] = "http/protobuf"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddVeriScanObservability(configuration, "VeriScan.Api");

        Assert.NotEmpty(services);
    }
}
