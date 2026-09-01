using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection;
using VeriScan.Api.Diagnostics;

namespace VeriScan.Api.Tests;

public sealed class RequestMetricsObservabilityTests
{
    [Fact]
    public void RecordsCountAndDurationWithRouteAndStatusDimensions()
    {
        using var listener = new MeterListener();
        var measurements = new List<MetricMeasurement>();
        listener.InstrumentPublished = (instrument, currentListener) =>
        {
            if (instrument.Meter.Name == RequestMetrics.MeterName)
            {
                currentListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, _) =>
        {
            if (instrument.Name == RequestMetrics.RequestCountName)
            {
                measurements.Add(new MetricMeasurement(measurement, CopyTags(tags)));
            }
        });
        listener.SetMeasurementEventCallback<double>((instrument, measurement, tags, _) =>
        {
            if (instrument.Name == RequestMetrics.RequestDurationName)
            {
                measurements.Add(new MetricMeasurement(measurement, CopyTags(tags)));
            }
        });
        listener.Start();

        using var provider = new ServiceCollection()
            .AddMetrics()
            .AddSingleton<RequestMetrics>()
            .BuildServiceProvider();
        var metrics = provider.GetRequiredService<RequestMetrics>();
        metrics.RecordRequest("POST", "/api/v1/moderation/batches", 202, 12.5);

        var count = Assert.Single(measurements, measurement =>
            measurement.Value == 1 && measurement.Tags["http.route"]?.ToString() == "/api/v1/moderation/batches");
        Assert.Equal(202, count.Tags["http.response.status_code"]);
        Assert.Contains(measurements, measurement =>
            measurement.Value == 12.5
            && measurement.Tags["http.request.method"]?.ToString() == "POST");
    }

    private static Dictionary<string, object?> CopyTags(
        ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        var copy = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var tag in tags)
        {
            copy[tag.Key] = tag.Value;
        }

        return copy;
    }

    private sealed record MetricMeasurement(
        double Value,
        IReadOnlyDictionary<string, object?> Tags)
    {
        public MetricMeasurement(long value, IReadOnlyDictionary<string, object?> tags)
            : this((double)value, tags)
        {
        }
    }
}
