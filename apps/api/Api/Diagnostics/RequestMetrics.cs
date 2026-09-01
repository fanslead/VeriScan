using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace VeriScan.Api.Diagnostics;

public sealed class RequestMetrics
{
    public const string MeterName = "VeriScan.Api";

    public const string RequestCountName = "veriscan.http.server.requests";

    public const string RequestDurationName = "veriscan.http.server.request.duration";

    private readonly Counter<long> requestCount;
    private readonly Histogram<double> requestDuration;

    public RequestMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);
        requestCount = meter.CreateCounter<long>(
            RequestCountName,
            unit: "{request}",
            description: "HTTP 请求完成总数。");
        requestDuration = meter.CreateHistogram<double>(
            RequestDurationName,
            unit: "ms",
            description: "HTTP 请求完成耗时。");
    }

    public void RecordRequest(string method, string route, int statusCode, double elapsedMilliseconds)
    {
        var tags = new TagList
        {
            { "http.request.method", method },
            { "http.route", route },
            { "http.response.status_code", statusCode }
        };

        requestCount.Add(1, tags);
        requestDuration.Record(elapsedMilliseconds, tags);
    }
}
