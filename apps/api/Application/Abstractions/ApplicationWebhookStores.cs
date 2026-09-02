using VeriScan.Domain.Entities;

namespace VeriScan.Application.Abstractions;

/// <summary>应用 Webhook 配置与连接测试的同库写入边界。</summary>
public interface IApplicationWebhookStore
{
    Task<ApplicationEntity?> GetApplicationAsync(
        Guid applicationId,
        CancellationToken cancellationToken);

    Task<ApplicationWebhook?> GetByApplicationAsync(
        Guid applicationId,
        CancellationToken cancellationToken);

    Task AddAsync(ApplicationWebhook webhook, CancellationToken cancellationToken);

    Task AddPublicationAsync(
        WebhookPublication publication,
        CancellationToken cancellationToken);

    Task<WebhookPublication?> GetTestAsync(
        Guid applicationId,
        Guid testId,
        CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}

/// <summary>审核终态写入 Webhook 发布事件的边界，方法本身不提交事务。</summary>
public interface IWebhookPublicationService
{
    Task EnqueueModerationTerminalAsync(
        ModerationRequest request,
        CancellationToken cancellationToken);
}

/// <summary>Webhook 发布 Worker 的租约与状态持久化边界。</summary>
public interface IWebhookPublicationStore
{
    Task<IReadOnlyList<WebhookPublication>> ClaimAvailableAsync(
        DateTimeOffset now,
        int limit,
        TimeSpan leaseDuration,
        string leaseOwner,
        CancellationToken cancellationToken);

    Task<ApplicationWebhook?> GetConfigurationAsync(
        Guid applicationWebhookId,
        CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
