using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;
using VeriScan.Application.Abstractions;
using VeriScan.Domain.Entities;

namespace VeriScan.Api.Workers;

/// <summary>Webhook 发布队列消费参数。</summary>
public sealed class WebhookPublicationWorkerOptions
{
    public const string SectionName = "WebhookPublication:Worker";

    /// <summary>是否启用后台发布。</summary>
    public bool Enabled { get; set; } = true;

    [Range(1, 1000)]
    public int BatchSize { get; set; } = 50;

    [Range(5, 600)]
    public int LeaseSeconds { get; set; } = 60;

    [Range(50, 10_000)]
    public int PollDelayMilliseconds { get; set; } = 500;

    [Range(1, 100)]
    public int MaximumPublishAttempts { get; set; } = 8;

    [Range(1, 3600)]
    public int MaximumFailureBackoffSeconds { get; set; } = 300;

    [Range(100, 60_000)]
    public int TestPollDelayMilliseconds { get; set; } = 1000;

    [Range(1, 600)]
    public int TestTimeoutSeconds { get; set; } = 30;
}

/// <summary>把同库发布队列中的事件幂等提交给 Webhook 供应商。</summary>
public sealed partial class WebhookPublicationWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<WebhookPublicationWorkerOptions> options,
    ILogger<WebhookPublicationWorker> logger) : BackgroundService
{
    private readonly string workerId = $"{Environment.MachineName}:{Guid.CreateVersion7():N}";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var processed = await ProcessBatchAsync(stoppingToken);
                if (processed == 0)
                {
                    await Task.Delay(
                        TimeSpan.FromMilliseconds(options.Value.PollDelayMilliseconds),
                        stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                LogLoopFailure(logger, exception);
                await Task.Delay(GetFailureBackoff(1), stoppingToken);
            }
        }
    }

    internal async Task<int> ProcessBatchAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IWebhookPublicationStore>();
        var provider = scope.ServiceProvider.GetRequiredService<IWebhookProvider>();
        var publications = await store.ClaimAvailableAsync(
            DateTimeOffset.UtcNow,
            options.Value.BatchSize,
            TimeSpan.FromSeconds(options.Value.LeaseSeconds),
            workerId,
            cancellationToken);

        foreach (var publication in publications)
        {
            await ProcessPublicationAsync(publication, store, provider, cancellationToken);
        }

        return publications.Count;
    }

    private async Task ProcessPublicationAsync(
        WebhookPublication publication,
        IWebhookPublicationStore store,
        IWebhookProvider provider,
        CancellationToken cancellationToken)
    {
        try
        {
            if (publication.ProviderMessageId is null)
            {
                await PublishAsync(publication, provider, cancellationToken);
                if (publication.Kind == WebhookPublicationKind.Test)
                {
                    publication.ScheduleTestPoll(
                        DateTimeOffset.UtcNow,
                        TimeSpan.FromMilliseconds(options.Value.TestPollDelayMilliseconds));
                }

                await store.SaveChangesAsync(cancellationToken);
                return;
            }

            await PollTestAsync(publication, store, provider, cancellationToken);
            await store.SaveChangesAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            await HandleFailureAsync(publication, store, exception, cancellationToken);
        }
    }

    private static async Task PublishAsync(
        WebhookPublication publication,
        IWebhookProvider provider,
        CancellationToken cancellationToken)
    {
        var attemptedAt = DateTimeOffset.UtcNow;
        publication.RecordSubmissionAttempt(attemptedAt);
        var receipt = await provider.PublishAsync(
            publication.ProviderApplicationId,
            publication.Id,
            publication.EventType,
            publication.PayloadJson,
            cancellationToken);
        publication.MarkProviderAccepted(receipt.ProviderMessageId, DateTimeOffset.UtcNow);
    }

    private async Task PollTestAsync(
        WebhookPublication publication,
        IWebhookPublicationStore store,
        IWebhookProvider provider,
        CancellationToken cancellationToken)
    {
        if (publication.Kind != WebhookPublicationKind.Test)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        if (now >= publication.CreatedAt.AddSeconds(options.Value.TestTimeoutSeconds))
        {
            await CompleteTestAsync(
                publication,
                store,
                WebhookTestOutcome.Failed,
                "webhook_test_timeout",
                null,
                null,
                now,
                cancellationToken);
            return;
        }

        var attempt = await provider.GetAttemptAsync(
            publication.ProviderApplicationId,
            publication.ProviderMessageId!,
            publication.ProviderEndpointId,
            cancellationToken);
        if (attempt.State == WebhookAttemptState.Pending)
        {
            publication.ScheduleTestPoll(
                now,
                TimeSpan.FromMilliseconds(options.Value.TestPollDelayMilliseconds));
            return;
        }

        await CompleteTestAsync(
            publication,
            store,
            attempt.State == WebhookAttemptState.Succeeded
                ? WebhookTestOutcome.Succeeded
                : WebhookTestOutcome.Failed,
            attempt.FailureCode,
            attempt.HttpStatusCode,
            attempt.LatencyMilliseconds,
            now,
            cancellationToken);
    }

    private static async Task CompleteTestAsync(
        WebhookPublication publication,
        IWebhookPublicationStore store,
        WebhookTestOutcome outcome,
        string? failureCode,
        int? statusCode,
        long? latencyMilliseconds,
        DateTimeOffset completedAt,
        CancellationToken cancellationToken)
    {
        if (outcome == WebhookTestOutcome.Succeeded)
        {
            publication.MarkTestSucceeded(statusCode, latencyMilliseconds, completedAt);
        }
        else
        {
            publication.MarkTestFailed(
                failureCode ?? "webhook_delivery_failed",
                statusCode,
                latencyMilliseconds,
                completedAt);
        }

        var configuration = await store.GetConfigurationAsync(
            publication.ApplicationWebhookId,
            cancellationToken);
        configuration?.RecordTestResult(
            publication.Id,
            publication.ConfigurationRevision,
            outcome,
            statusCode,
            latencyMilliseconds,
            completedAt);
    }

    private async Task HandleFailureAsync(
        WebhookPublication publication,
        IWebhookPublicationStore store,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var failedAt = DateTimeOffset.UtcNow;
        var failureCode = GetFailureCode(exception);
        LogPublicationFailure(
            logger,
            exception,
            publication.Id,
            publication.EventType,
            publication.AttemptCount,
            failureCode);

        if (publication.ProviderMessageId is not null &&
            publication.Kind == WebhookPublicationKind.Test &&
            failedAt < publication.CreatedAt.AddSeconds(options.Value.TestTimeoutSeconds))
        {
            publication.ScheduleTestPoll(
                failedAt,
                TimeSpan.FromMilliseconds(options.Value.TestPollDelayMilliseconds));
        }
        else
        {
            publication.RetryOrDeadLetter(
                failureCode,
                failedAt,
                GetFailureBackoff(publication.AttemptCount),
                options.Value.MaximumPublishAttempts);
            if (publication.Kind == WebhookPublicationKind.Test &&
                publication.Status == WebhookPublicationStatus.DeadLetter)
            {
                await CompleteDeadLetteredTestAsync(
                    publication,
                    store,
                    failedAt,
                    cancellationToken);
            }
        }

        await store.SaveChangesAsync(cancellationToken);
    }

    private static async Task CompleteDeadLetteredTestAsync(
        WebhookPublication publication,
        IWebhookPublicationStore store,
        DateTimeOffset completedAt,
        CancellationToken cancellationToken)
    {
        var configuration = await store.GetConfigurationAsync(
            publication.ApplicationWebhookId,
            cancellationToken);
        configuration?.RecordTestResult(
            publication.Id,
            publication.ConfigurationRevision,
            WebhookTestOutcome.Failed,
            null,
            null,
            completedAt);
    }

    private TimeSpan GetFailureBackoff(int attemptCount)
    {
        var exponent = Math.Clamp(attemptCount - 1, 0, 10);
        var seconds = Math.Min(
            Math.Pow(2, exponent),
            options.Value.MaximumFailureBackoffSeconds);
        return TimeSpan.FromSeconds(seconds);
    }

    private static string GetFailureCode(Exception exception)
    {
        return exception switch
        {
            RequestTimeoutException => "webhook_provider_timeout",
            WebhookProviderUnavailableException => "webhook_provider_unavailable",
            ApplicationBaseException applicationException => applicationException.ErrorCode,
            _ => "webhook_publication_failed"
        };
    }

    [LoggerMessage(
        EventId = 42_100,
        Level = LogLevel.Warning,
        Message = "Webhook 发布 Worker 轮询失败。")]
    private static partial void LogLoopFailure(ILogger logger, Exception exception);

    [LoggerMessage(
        EventId = 42_101,
        Level = LogLevel.Warning,
        Message = "Webhook 事件 {PublicationId} 类型 {EventType} 第 {AttemptCount} 次处理失败，错误码 {FailureCode}。")]
    private static partial void LogPublicationFailure(
        ILogger logger,
        Exception exception,
        Guid publicationId,
        string eventType,
        int attemptCount,
        string failureCode);
}
