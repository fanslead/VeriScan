using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;
using VeriScan.Application.Abstractions;
using VeriScan.Domain.Entities;

namespace VeriScan.Api.Workers;

/// <summary>Outbox 后台消费参数。</summary>
public sealed class OutboxWorkerOptions
{
    public const string SectionName = "Outbox:Worker";

    /// <summary>是否启用后台消费，测试环境可关闭。</summary>
    public bool Enabled { get; set; } = true;

    [Range(1, 1000)]
    public int BatchSize { get; set; } = 100;

    [Range(5, 600)]
    public int LeaseSeconds { get; set; } = 60;

    [Range(50, 10000)]
    public int PollDelayMilliseconds { get; set; } = 500;

    [Range(1, 3600)]
    public int MaximumFailureBackoffSeconds { get; set; } = 300;
}

/// <summary>负责领取、处理和确认 Outbox 事件的后台 Worker。</summary>
public sealed partial class OutboxWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<OutboxWorkerOptions> options,
    ILogger<OutboxWorker> logger) : BackgroundService
{
    private const string ConsumerName = "outbox-usage-projection-v1";
    private const string UsageEventCompleted = "moderation.completed";
    private const string UsageEventCancelled = "moderation.cancelled";
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
                await Task.Delay(
                    GetFailureBackoff(1),
                    stoppingToken);
            }
        }
    }

    private async Task<int> ProcessBatchAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var outboxStore = scope.ServiceProvider.GetRequiredService<IOutboxStore>();
        var events = await outboxStore.ClaimAvailableAsync(
            DateTimeOffset.UtcNow,
            options.Value.BatchSize,
            TimeSpan.FromSeconds(options.Value.LeaseSeconds),
            workerId,
            cancellationToken);
        if (events.Count == 0)
        {
            return 0;
        }

        var usageProjection = scope.ServiceProvider.GetRequiredService<IUsageProjectionService>();
        var usageEvents = events
            .Where(IsUsageEvent)
            .GroupBy(outboxEvent => outboxEvent.ApplicationId)
            .ToArray();
        foreach (var applicationEvents in usageEvents)
        {
            await ProcessUsageEventsAsync(
                applicationEvents,
                usageProjection,
                outboxStore,
                cancellationToken);
        }

        foreach (var outboxEvent in events.Where(outboxEvent => !IsUsageEvent(outboxEvent)))
        {
            await CompleteOrFailAsync(outboxEvent, outboxStore, cancellationToken);
        }

        return events.Count;
    }

    private async Task ProcessUsageEventsAsync(
        IGrouping<Guid?, OutboxEvent> applicationEvents,
        IUsageProjectionService usageProjection,
        IOutboxStore outboxStore,
        CancellationToken cancellationToken)
    {
        var applicationId = applicationEvents.Key;
        try
        {
            if (applicationId is null || applicationId.Value == Guid.Empty)
            {
                throw new InvalidOperationException("审核 Outbox 缺少应用标识。");
            }

            await usageProjection.RebuildAsync(
                applicationId.Value,
                null,
                null,
                null,
                cancellationToken);
            foreach (var outboxEvent in applicationEvents)
            {
                await CompleteOrFailAsync(outboxEvent, outboxStore, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            foreach (var outboxEvent in applicationEvents)
            {
                await FailAsync(outboxEvent, outboxStore, exception, cancellationToken);
            }
        }
    }

    private async Task CompleteOrFailAsync(
        OutboxEvent outboxEvent,
        IOutboxStore outboxStore,
        CancellationToken cancellationToken)
    {
        try
        {
            _ = await outboxStore.TryCompleteAsync(
                outboxEvent.Id,
                workerId,
                ConsumerName,
                DateTimeOffset.UtcNow,
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            await FailAsync(outboxEvent, outboxStore, exception, cancellationToken);
        }
    }

    private async Task FailAsync(
        OutboxEvent outboxEvent,
        IOutboxStore outboxStore,
        Exception exception,
        CancellationToken cancellationToken)
    {
        LogEventFailure(
            logger,
            exception,
            outboxEvent.Id,
            outboxEvent.EventType,
            outboxEvent.AttemptCount);
        var availableAt = DateTimeOffset.UtcNow.Add(GetFailureBackoff(outboxEvent.AttemptCount));
        _ = await outboxStore.TryFailAsync(
            outboxEvent.Id,
            workerId,
            GetFailureCode(exception),
            availableAt,
            cancellationToken);
    }

    private TimeSpan GetFailureBackoff(int attemptCount)
    {
        var exponent = Math.Clamp(attemptCount - 1, 0, 10);
        var seconds = Math.Min(
            Math.Pow(2, exponent),
            options.Value.MaximumFailureBackoffSeconds);
        return TimeSpan.FromSeconds(seconds);
    }

    private static bool IsUsageEvent(OutboxEvent outboxEvent)
    {
        return outboxEvent.EventType is UsageEventCompleted or UsageEventCancelled;
    }

    private static string GetFailureCode(Exception exception)
    {
        return exception switch
        {
            RequestValidationException => "OUTBOX_VALIDATION_FAILED",
            ResourceNotFoundException => "OUTBOX_RESOURCE_NOT_FOUND",
            RequestConflictException => "OUTBOX_CONFLICT",
            _ => "OUTBOX_PROCESSING_FAILED"
        };
    }

    [LoggerMessage(
        EventId = 3100,
        Level = LogLevel.Warning,
        Message = "Outbox Worker 轮询失败。")]
    private static partial void LogLoopFailure(ILogger logger, Exception exception);

    [LoggerMessage(
        EventId = 3101,
        Level = LogLevel.Warning,
        Message = "Outbox 事件 {OutboxEventId} 类型 {EventType} 第 {AttemptCount} 次处理失败。")]
    private static partial void LogEventFailure(
        ILogger logger,
        Exception exception,
        Guid outboxEventId,
        string eventType,
        int attemptCount);
}
