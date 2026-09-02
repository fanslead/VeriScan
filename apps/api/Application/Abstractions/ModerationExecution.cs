namespace VeriScan.Application.Abstractions;

public interface IModerationExecutionPolicy
{
    int MaximumConcurrentAiCalls { get; }

    TimeSpan SynchronousDeadline { get; }
}

public interface IModerationQueuePolicy
{
    int MaximumSyncItems { get; }

    int MaximumAsyncItems { get; }

    int AutoAsyncItemThreshold { get; }

    int AutoAsyncAiThreshold { get; }

    int MaximumAttempts { get; }

    TimeSpan LeaseDuration { get; }

    TimeSpan EmptyQueueDelay { get; }

    TimeSpan GetRetryDelay(int attemptCount);
}

/// <summary>审核写操作的幂等保留策略。</summary>
public interface IModerationIdempotencyPolicy
{
    TimeSpan OperationRetention { get; }
}
