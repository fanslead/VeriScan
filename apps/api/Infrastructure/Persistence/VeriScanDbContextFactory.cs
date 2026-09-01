using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace VeriScan.Infrastructure.Persistence;

public sealed class VeriScanDbContextFactory : IDesignTimeDbContextFactory<VeriScanDbContext>
{
    public VeriScanDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__VeriScan");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "执行 EF Core 迁移前必须设置 ConnectionStrings__VeriScan 环境变量。");
        }

        var options = new DbContextOptionsBuilder<VeriScanDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        return new VeriScanDbContext(options);
    }
}
