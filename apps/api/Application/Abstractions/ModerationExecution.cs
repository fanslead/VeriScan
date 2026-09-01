namespace VeriScan.Application.Abstractions;

public interface IModerationExecutionPolicy
{
    int MaximumConcurrentAiCalls { get; }
}
