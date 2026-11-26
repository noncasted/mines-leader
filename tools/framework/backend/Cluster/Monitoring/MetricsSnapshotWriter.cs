using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using System.Text.Json;
using Common.Extensions;
using Microsoft.Extensions.Logging;

namespace Cluster.Monitoring;

// Listens to BackendMeter in the whole process (the meter is static) and writes metrics_<name>.json into the directory
// every 10 seconds and once more when disposed. One writer per process: two writers of one process would both hold
// the totals of the process.
public sealed class MetricsSnapshotWriter : IDisposable
{
    public MetricsSnapshotWriter(ILogger logger, string directory, string serviceName)
    {
        _logger = logger;
        _serviceName = serviceName;
        _path = Path.Combine(directory, $"metrics_{serviceName}.json");

        _listener = new MeterListener();
        _listener.InstrumentPublished = OnInstrumentPublished;
        _listener.SetMeasurementEventCallback<long>(OnMeasurement);
        _listener.SetMeasurementEventCallback<int>(OnMeasurement);
        _listener.SetMeasurementEventCallback<double>(OnMeasurement);
        _listener.Start();

        _timer = new Timer(WriteSnapshot, null, Period, Period);
    }

    private static readonly TimeSpan Period = TimeSpan.FromSeconds(10);

    private readonly ILogger _logger;
    private readonly string _serviceName;
    private readonly string _path;
    private readonly MeterListener _listener;
    private readonly Timer _timer;
    private readonly ConcurrentDictionary<string, MetricSnapshot> _snapshots = new();

    // A tick of the timer may still run while Dispose writes the last snapshot.
    private readonly Lock _writeLock = new();

    // Stops listening and writes the last snapshot.
    public void Dispose()
    {
        _timer.Dispose();
        _listener.Dispose();
        WriteSnapshot(null);
    }

    private void OnInstrumentPublished(Instrument instrument, MeterListener listener)
    {
        if (instrument.Meter.Name == BackendMeter.Name)
            listener.EnableMeasurementEvents(instrument);
    }

    private void OnMeasurement(
        Instrument instrument,
        long value,
        ReadOnlySpan<KeyValuePair<string, object?>> tags,
        object? state)
    {
        Record(instrument, value);
    }

    private void OnMeasurement(
        Instrument instrument,
        int value,
        ReadOnlySpan<KeyValuePair<string, object?>> tags,
        object? state)
    {
        Record(instrument, value);
    }

    private void OnMeasurement(
        Instrument instrument,
        double value,
        ReadOnlySpan<KeyValuePair<string, object?>> tags,
        object? state)
    {
        Record(instrument, value);
    }

    private void Record(Instrument instrument, double value)
    {
        var snapshot = _snapshots.GetOrAdd(instrument.Name, _ => new MetricSnapshot(instrument));
        snapshot.Record(value);
    }

    private void WriteSnapshot(object? state)
    {
        try
        {
            var metrics = new Dictionary<string, object>();

            foreach (var (name, snapshot) in _snapshots)
            {
                metrics[name] = snapshot.ToDict();
            }

            var data = new Dictionary<string, object>
            {
                ["timestamp"] = DateTime.UtcNow.ToString("o"),
                ["service"] = _serviceName,
                ["metrics"] = metrics
            };

            var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });

            lock (_writeLock)
                File.WriteAllText(_path, json);
        }
        catch (Exception e)
        {
            // The boundary of the timer: a failed write is logged, the next tick writes again.
            _logger.LogError(e, "[MetricsSnapshot] Failed to write snapshot");
        }
    }

    private class MetricSnapshot
    {
        private readonly bool _isHistogram;
        private long _count;
        private double _sum;
        private double _min = double.MaxValue;
        private double _max = double.MinValue;
        private readonly Lock _lock = new();

        public MetricSnapshot(Instrument instrument)
        {
            _isHistogram = instrument is Histogram<double> or Histogram<int> or Histogram<long>;
        }

        public void Record(double value)
        {
            lock (_lock)
            {
                _count++;
                _sum += value;

                if (value < _min)
                    _min = value;

                if (value > _max)
                    _max = value;
            }
        }

        public Dictionary<string, object> ToDict()
        {
            lock (_lock)
            {
                if (_isHistogram)
                {
                    return new Dictionary<string, object>
                    {
                        ["count"] = _count,
                        ["sum"] = Math.Round(_sum, 2),
                        ["min"] = _count > 0 ? Math.Round(_min, 2) : 0,
                        ["max"] = _count > 0 ? Math.Round(_max, 2) : 0,
                        ["avg"] = _count > 0 ? Math.Round(_sum / _count, 2) : 0
                    };
                }

                return new Dictionary<string, object>
                {
                    ["value"] = _count > 0 && _sum == _count ? (long)_sum : Math.Round(_sum, 2)
                };
            }
        }
    }
}