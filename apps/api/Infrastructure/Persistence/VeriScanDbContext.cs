using Microsoft.EntityFrameworkCore;
using VeriScan.Domain.Entities;

namespace VeriScan.Infrastructure.Persistence;

public sealed class VeriScanDbContext(DbContextOptions<VeriScanDbContext> options) : DbContext(options)
{
    public DbSet<ApplicationEntity> Applications => Set<ApplicationEntity>();

    public DbSet<ApplicationApiKey> ApplicationApiKeys => Set<ApplicationApiKey>();

    public DbSet<ModerationRequest> ModerationRequests => Set<ModerationRequest>();

    public DbSet<ModerationItem> ModerationItems => Set<ModerationItem>();

    public DbSet<WordRule> WordRules => Set<WordRule>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(VeriScanDbContext).Assembly);
    }
}
