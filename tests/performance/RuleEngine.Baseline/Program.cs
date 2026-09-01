using System.Diagnostics;
using System.Text.Json;
using VeriScan.Application.Services;
using VeriScan.Domain.Entities;

var ruleCount = ReadPositiveArgument(args, 0, 10_000);
var iterations = ReadPositiveArgument(args, 1, 20_000);
var warmupIterations = Math.Min(2_000, iterations);
var rules = Enumerable.Range(0, ruleCount)
    .Select(index => new WordRule(
        Guid.Empty,
        $"风险词{index:D6}",
        WordRuleType.Suspicious,
        "benchmark",
        0.6m))
    .ToArray();

var engine = new RuleModerationEngine();
var compileStopwatch = Stopwatch.StartNew();
var policy = engine.GetOrCompile("ruleset@performance-baseline", rules);
compileStopwatch.Stop();

var content = $"这是一段用于本地性能基线的普通文本，末尾包含风险词{ruleCount - 1:D6}。";
for (var index = 0; index < warmupIterations; index++)
{
    _ = policy.Evaluate(content);
}

GC.Collect();
GC.WaitForPendingFinalizers();
GC.Collect();
var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
var samples = new long[iterations];
for (var index = 0; index < iterations; index++)
{
    var startedAt = Stopwatch.GetTimestamp();
    _ = policy.Evaluate(content);
    samples[index] = Stopwatch.GetTimestamp() - startedAt;
}

var allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
Array.Sort(samples);
var totalTicks = samples.Sum();
var result = new
{
    runtime = Environment.Version.ToString(),
    processorCount = Environment.ProcessorCount,
    ruleCount,
    contentCharacters = content.Length,
    iterations,
    compileMilliseconds = Math.Round(compileStopwatch.Elapsed.TotalMilliseconds, 3),
    meanMicroseconds = Math.Round(ToMicroseconds(totalTicks / (double)iterations), 3),
    p50Microseconds = Math.Round(ToMicroseconds(Percentile(samples, 0.50)), 3),
    p95Microseconds = Math.Round(ToMicroseconds(Percentile(samples, 0.95)), 3),
    p99Microseconds = Math.Round(ToMicroseconds(Percentile(samples, 0.99)), 3),
    operationsPerSecond = Math.Round(iterations / (totalTicks / (double)Stopwatch.Frequency), 0),
    allocatedBytesPerOperation = Math.Round(allocatedBytes / (double)iterations, 1),
    note = "本地进程内规则引擎基线，不含 HTTP、数据库、API Key、外部 AI 与网络耗时。"
};

Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));

static int ReadPositiveArgument(string[] arguments, int index, int fallback)
{
    return arguments.Length > index && int.TryParse(arguments[index], out var parsed) && parsed > 0
        ? parsed
        : fallback;
}

static double Percentile(long[] samples, double percentile)
{
    var index = (int)Math.Ceiling(percentile * samples.Length) - 1;
    return samples[Math.Clamp(index, 0, samples.Length - 1)];
}

static double ToMicroseconds(double ticks)
{
    return ticks * 1_000_000d / Stopwatch.Frequency;
}
