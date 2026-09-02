using VeriScan.Application.Abstractions;
using VeriScan.Application.Services;
using VeriScan.Domain.Entities;

namespace VeriScan.Api.Workers;

public sealed partial class ModerationJobWorker(
    IServiceScopeFactory scopeFactory,
    IModerationQueuePolicy queuePolicy,
    ILogger<ModerationJobWorker> logger) : BackgroundService
{
    private readonly string _workerId = $"{Environment.MachineName}:{Guid.NewGuid():N}";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var processed = await TryProcessNextAsync(stoppingToken);
            if (!processed)
            {
                await Task.Delay(queuePolicy.EmptyQueueDelay, stoppingToken);
            }
        }
    }

    private async Task<bool> TryProcessNextAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var jobStore = scope.ServiceProvider.GetRequiredService<IModerationJobStore>();
        var service = scope.ServiceProvider.GetRequiredService<IModerationService>();
        var job = await jobStore.ClaimNextAsync(
            _workerId,
            DateTimeOffset.UtcNow,
            queuePolicy.LeaseDuration,
            cancellationToken);
        if (job is null)
        {
            return false;
        }

        try
        {
            await service.ProcessQueuedBatchAsync(job.RequestId, cancellationToken);
            job.Complete(DateTimeOffset.UtcNow);
            await jobStore.SaveChangesAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            LogProcessingFailure(logger, job.Id, job.AttemptCount, exception.GetType().Name);
            var failedAt = DateTimeOffset.UtcNow;
            job.Retry(
                "ASYNC_PROCESSING_FAILED",
                failedAt,
                queuePolicy.GetRetryDelay(job.AttemptCount));
            if (job.Status == ModerationJobStatus.DeadLetter)
            {
                foreach (var item in job.Request?.Items.Where(item =>
                             item.ProcessingStatus is not (
                                 ModerationProcessingStatus.Completed or
                                 ModerationProcessingStatus.Failed or
                                 ModerationProcessingStatus.Cancelled)) ?? [])
                {
                    item.Fail("ASYNC_RETRY_EXHAUSTED", failedAt);
                }

                job.Request?.Complete(failedAt);
                await service.FinalizeDeadLetterAsync(job.RequestId, cancellationToken);
            }
            else
            {
                job.Request?.MarkRetryWait();
                foreach (var item in job.Request?.Items ?? [])
                {
                    item.MarkRetryWait();
                }
            }

            await jobStore.SaveChangesAsync(cancellationToken);
        }

        return true;
    }

    [LoggerMessage(
        EventId = 2001,
        Level = LogLevel.Warning,
        Message = "异步审核任务 {JobId} 第 {AttemptCount} 次执行失败，异常类型 {ExceptionType}")]
    private static partial void LogProcessingFailure(
        ILogger logger,
        Guid jobId,
        int attemptCount,
        string exceptionType);
}
