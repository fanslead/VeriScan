using System.Diagnostics.Metrics;

namespace VeriScan.Infrastructure.ExternalAi;

public static class ExternalAiMetrics
{
    public const string MeterName = "VeriScan.ExternalAi";

    private static readonly Meter Meter = new(MeterName, "1.0.0");
    private static readonly Counter<long> RequestCount = Meter.CreateCounter<long>(
        "veriscan.external_ai.requests",
        unit: "{request}");
    private static readonly Counter<long> RetryCount = Meter.CreateCounter<long>(
        "veriscan.external_ai.retries",
        unit: "{retry}");
    private static readonly Counter<long> TimeoutCount = Meter.CreateCounter<long>(
        "veriscan.external_ai.timeouts",
        unit: "{timeout}");
    private static readonly Counter<long> CircuitOpenCount = Meter.CreateCounter<long>(
        "veriscan.external_ai.circuit_open",
        unit: "{event}");
    private static readonly Histogram<double> RequestDuration = Meter.CreateHistogram<double>(
        "veriscan.external_ai.request.duration",
        unit: "ms");

    public static void RecordRequest(string protocol, string outcome)
    {
        RequestCount.Add(
            1,
            new KeyValuePair<string, object?>("ai.protocol", protocol),
            new KeyValuePair<string, object?>("outcome", outcome));
    }

    public static void RecordRetry(string protocol, int statusCode)
    {
        RetryCount.Add(
            1,
            new KeyValuePair<string, object?>("ai.protocol", protocol),
            new KeyValuePair<string, object?>("http.status_code", statusCode));
    }

    public static void RecordTimeout(string protocol)
    {
        TimeoutCount.Add(1, new KeyValuePair<string, object?>("ai.protocol", protocol));
    }

    public static void RecordCircuitOpened(string protocol)
    {
        CircuitOpenCount.Add(1, new KeyValuePair<string, object?>("ai.protocol", protocol));
    }

    public static void RecordDuration(string protocol, double elapsedMilliseconds)
    {
        RequestDuration.Record(
            elapsedMilliseconds,
            new KeyValuePair<string, object?>("ai.protocol", protocol));
    }
}
