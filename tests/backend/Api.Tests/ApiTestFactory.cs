using System.Collections.Generic;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using VeriScan.Api.Authentication;
using VeriScan.Application.Abstractions;
using VeriScan.Application.Services;
using VeriScan.Domain.Entities;
using VeriScan.Infrastructure.Persistence;

namespace VeriScan.Api.Tests;

public sealed class ApiTestFactory : WebApplicationFactory<Program>
{
    private readonly string databaseName = $"veriscan-tests-{Guid.CreateVersion7():N}";
    private readonly Action<IServiceCollection>? additionalServices;

    public ApiTestFactory()
    {
    }

    internal ApiTestFactory(Action<IServiceCollection> additionalServices)
    {
        this.additionalServices = additionalServices;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Security:ApiKey:Pepper"] = "test-only-pepper-with-at-least-32-bytes-0001",
                ["Security:ApiKey:PepperVersion"] = "test-v1",
                ["Security:AiCredentials:MasterKey"] = "dmVyaXNjYW4tdGVzdC1tYXN0ZXIta2V5LTMyLWJ5dGU=",
                ["Database:AutoMigrate"] = "false"
            });
        });
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<VeriScanDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<VeriScanDbContext>>();
            services.AddDbContext<VeriScanDbContext>(options =>
                options.UseInMemoryDatabase(databaseName));
            services.RemoveAll<IAiEndpointPolicy>();
            services.RemoveAll<IAiConfigurationProbe>();
            services.RemoveAll<IModerationAiClient>();
            services.RemoveAll<IAiSchemaDescriptor>();
            services.AddSingleton<IAiEndpointPolicy, TestAiEndpointPolicy>();
            services.AddSingleton<IAiConfigurationProbe, TestAiConfigurationProbe>();
            services.AddSingleton<IModerationAiClient, TestModerationAiClient>();
            services.AddSingleton<IAiSchemaDescriptor, TestAiSchemaDescriptor>();
            services.AddAuthentication()
                .AddScheme<AuthenticationSchemeOptions, TestAdminAuthenticationHandler>(
                    "TestAdmin",
                    _ => { });
            services.Configure<Microsoft.AspNetCore.Authorization.AuthorizationOptions>(options =>
            {
                options.AddPolicy(
                    AdminJwtOptions.Policy,
                    new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder("TestAdmin")
                        .RequireAuthenticatedUser()
                        .AddRequirements(new AdminRoleRequirement())
                        .Build());
            });
            additionalServices?.Invoke(services);
        });
    }

    public async Task SeedRulesAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<VeriScanDbContext>();
        await dbContext.Database.EnsureCreatedAsync();
        if (await dbContext.RuleSetVersions.AnyAsync())
        {
            return;
        }

        var ruleSet = new RuleSetVersion("测试基础规则");
        ruleSet.ReplaceDraft(
            ruleSet.Name,
            [
                new WordRule(ruleSet.Id, "赌博", WordRuleType.Black, "gambling", 1.0m),
                new WordRule(ruleSet.Id, "加微信", WordRuleType.Suspicious, "contact", 0.6m),
                new WordRule(ruleSet.Id, "明鉴", WordRuleType.White, "product", 0.1m)
            ]);
        var seedChecksum = RuleSetPolicyValidator.ComputeChecksum(ruleSet.Name, ruleSet.Rules);
        ruleSet.RecordSuccessfulValidation(seedChecksum, DateTimeOffset.UtcNow);
        ruleSet.Publish(seedChecksum, DateTimeOffset.UtcNow);
        dbContext.RuleSetVersions.Add(ruleSet);
        await dbContext.SaveChangesAsync();
    }
}
