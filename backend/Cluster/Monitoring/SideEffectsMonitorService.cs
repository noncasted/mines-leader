using System.Diagnostics.Metrics;
using Cluster.Deploy;
using Common.Extensions;
using Common.Reactive;
using Infrastructure;
using Microsoft.Extensions.Logging;

namespace Cluster.Monitoring;

public class SideEffectsMonitorService : ILocalSetupCompleted
{
    public SideEffectsMonitorService(
        ILiveState<SideEffectsLiveData> liveData,
        ISideEffectsStorage storage,
        ILogger<SideEffectsMonitorService> logger)
    {
        _liveData = liveData;
        _storage = storage;
        _logger = logger;
    }

    private readonly ILiveState<SideEffectsLiveData> _liveData;
    private readonly ISideEffectsStorage _storage;
    private readonly ILogger<SideEffectsMonitorService> _logger;

    private long _processedAccumulator;
    private long _failedAccumulator;
    private readonly List<SideEffectsThroughputEntry> _history = new();
    private const int MaxHistorySize = 120;

    public Task OnLocalSetupCompleted(IReadOnlyLifetime lifetime)
    {
        var listener = new MeterListener();

        listener.InstrumentPublished = (instrument, meterListener) => {
            if (instrument.Meter.Name == "Backend" &&
                instrument.Name is "backend.side_effects.processed" or "backend.side_effects.failed")
                meterListener.EnableMeasurementEvents(instrument);
        };

        listener.SetMeasurementEventCallback<long>((instrument, value, _, _) => {
            if (instrument.Name == "backend.side_effects.processed")
                Interlocked.Add(ref _processedAccumulator, value);
            else if (instrument.Name == "backend.side_effects.failed")
                Interlocked.Add(ref _failedAccumulator, value);
        });

        listener.Start();
        lifetime.Listen(() => listener.Dispose());

        Loop(lifetime).NoAwait();
        return Task.CompletedTask;
    }

    private async Task Loop(IReadOnlyLifetime lifetime)
    {
        while (lifetime.IsTerminated == false)
        {
            try
            {
                var processed = Interlocked.Exchange(ref _processedAccumulator, 0);
                var failed = Interlocked.Exchange(ref _failedAccumulator, 0);

                _history.Add(new SideEffectsThroughputEntry
                {
                    Timestamp = DateTime.UtcNow,
                    Processed = processed,
                    Failed = failed
                });

                if (_history.Count > MaxHistorySize)
                    _history.RemoveAt(0);

                var stats = await _storage.GetStats();
                var retryEntries = await _storage.GetRetryEntries(50);

                await _liveData.SetValue(new SideEffectsLiveData
                {
                    QueueCount = stats.QueueCount,
                    ProcessingCount = stats.ProcessingCount,
                    RetryCount = stats.RetryCount,
                    DeadLetterCount = stats.DeadLetterCount,
                    ThroughputHistory = _history.ToList(),
                    RetryEntries = retryEntries.Select(e => new SideEffectsRetryEntry
                    {
                        Id = e.Id,
                        TypeName = e.TypeName,
                        RetryCount = e.RetryCount,
                        RetryAfter = e.RetryAfter,
                        CreatedAt = e.CreatedAt
                    }).ToList()
                });
            }
            catch (Exception e)
            {
                _logger.LogError(e, "[SideEffectsMonitor] Failed to update live data");
            }

            await Task.Delay(1000, lifetime.Token);
        }
    }
}