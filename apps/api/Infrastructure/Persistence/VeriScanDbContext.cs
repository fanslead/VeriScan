using Microsoft.EntityFrameworkCore;
using VeriScan.Domain.Entities;

namespace VeriScan.Infrastructure.Persistence;

public sealed class VeriScanDbContext(DbContextOptions<VeriScanDbContext> options) : DbContext(options)
{
    public DbSet<ApplicationEntity> Applications => Set<ApplicationEntity>();

    public DbSet<ApplicationApiKey> ApplicationApiKeys => Set<ApplicationApiKey>();

    public DbSet<ModerationRequest> ModerationRequests => Set<ModerationRequest>();

    public DbSet<ModerationItem> ModerationItems => Set<ModerationItem>();

    public DbSet<ModerationJob> ModerationJobs => Set<ModerationJob>();

    public DbSet<IdempotentOperation> IdempotentOperations => Set<IdempotentOperation>();

    public DbSet<ApplicationWebhook> ApplicationWebhooks => Set<ApplicationWebhook>();

    public DbSet<WebhookPublication> WebhookPublications => Set<WebhookPublication>();

    public DbSet<WordRule> WordRules => Set<WordRule>();

    public DbSet<RegexRule> RegexRules => Set<RegexRule>();

    public DbSet<CombinationRule> CombinationRules => Set<CombinationRule>();

    public DbSet<RuleSetVersion> RuleSetVersions => Set<RuleSetVersion>();

    public DbSet<AiModelConfiguration> AiModelConfigurations => Set<AiModelConfiguration>();

    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    public DbSet<OutboxEvent> OutboxEvents => Set<OutboxEvent>();

    public DbSet<ApiRequestEvent> ApiRequestEvents => Set<ApiRequestEvent>();

    public DbSet<AiInvocation> AiInvocations => Set<AiInvocation>();

    public DbSet<UsageHourly> UsageHourly => Set<UsageHourly>();

    public DbSet<UsageDaily> UsageDaily => Set<UsageDaily>();

    public DbSet<UsageConsumedEvent> UsageConsumedEvents => Set<UsageConsumedEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(VeriScanDbContext).Assembly);
    }
}
