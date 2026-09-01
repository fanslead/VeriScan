using Microsoft.EntityFrameworkCore;
using VeriScan.Application.Services;
using VeriScan.Infrastructure.Persistence;

namespace VeriScan.Api.Tests;

public sealed class RuleSetPolicyValidatorTests
{
    [Fact]
    public async Task InitializerPublishesChecksumComputedFromSeedContent()
    {
        var options = new DbContextOptionsBuilder<VeriScanDbContext>()
            .UseInMemoryDatabase($"seed-checksum-{Guid.CreateVersion7():N}")
            .Options;
        await using var dbContext = new VeriScanDbContext(options);
        var initializer = new DatabaseInitializer(dbContext);

        await initializer.InitializeAsync(CancellationToken.None);

        var ruleSet = await dbContext.RuleSetVersions.Include(item => item.Rules).SingleAsync();
        var expected = RuleSetPolicyValidator.ComputeChecksum(ruleSet.Name, ruleSet.Rules);
        Assert.Equal(expected, ruleSet.LastValidatedChecksum);
        Assert.Equal(expected, ruleSet.PublishedChecksum);
    }
}
