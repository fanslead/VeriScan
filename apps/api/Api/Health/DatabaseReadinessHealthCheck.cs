using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using VeriScan.Infrastructure.Persistence;

namespace VeriScan.Api.Health;

public sealed class DatabaseReadinessHealthCheck(IServiceScopeFactory scopeFactory) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<VeriScanDbContext>();
            if (!dbContext.Database.IsRelational())
            {
                return HealthCheckResult.Healthy("非关系型测试存储已就绪。");
            }

            if (!await dbContext.Database.CanConnectAsync(cancellationToken))
            {
                return HealthCheckResult.Unhealthy("PostgreSQL 当前不可连接。");
            }

            var pendingMigrations = await dbContext.Database
                .GetPendingMigrationsAsync(cancellationToken);
            var pendingCount = pendingMigrations.Count();
            return pendingCount == 0
                ? HealthCheckResult.Healthy("PostgreSQL 已连接且迁移已同步。")
                : HealthCheckResult.Unhealthy($"PostgreSQL 仍有 {pendingCount} 个待执行迁移。");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return HealthCheckResult.Unhealthy("PostgreSQL 就绪检查失败。", exception);
        }
    }
}
