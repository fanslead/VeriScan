using System.Net;
using System.Net.Http.Json;

namespace VeriScan.Api.Tests;

public sealed class HealthEndpointsTests
{
    [Fact]
    public async Task LivenessAndReadinessAreSeparateAndAnonymous()
    {
        await using var factory = new ApiTestFactory();
        using var client = factory.CreateClient();

        var liveness = await client.GetAsync("/healthz");
        var readiness = await client.GetAsync("/readyz");
        var readinessBody = await readiness.Content.ReadFromJsonAsync<HealthResponse>();

        Assert.Equal(HttpStatusCode.OK, liveness.StatusCode);
        Assert.Equal(HttpStatusCode.OK, readiness.StatusCode);
        Assert.Equal("healthy", readinessBody?.Status);
        var check = Assert.Single(readinessBody?.Checks ?? []);
        Assert.Equal("postgres", check.Name);
        Assert.Equal("healthy", check.Status);
    }

    private sealed record HealthResponse(string Status, IReadOnlyList<HealthCheckResponse> Checks);

    private sealed record HealthCheckResponse(string Name, string Status, string? Description);
}
