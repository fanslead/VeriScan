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
