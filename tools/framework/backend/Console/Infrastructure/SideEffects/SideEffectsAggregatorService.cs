using Cluster.Coordination;
using Cluster.Discovery;
using Cluster.Monitoring;
using Common.Extensions;
using Internal;
using Infrastructure;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Console;

public class SideEffectsAggregatorService : IHostedService, ISideEffectsAggregator
{
    public SideEffectsAggregatorService(
        IMessaging messaging,
        IServiceDiscovery discovery,
        ILogger<SideEffectsAggregatorService> logger)
    {
        _messaging = messaging;
        _discovery = discovery;
        _logger = logger;
    }

    private readonly IMessaging _messaging;
    private readonly IServiceDiscovery _discovery;
    private readonly ILogger<SideEffectsAggregatorService> _logger;

    // Set from the loop on the thread pool, viewed by the pages from their circuits.
    private readonly SharedViewableProperty<SideEffectsAggregate> _aggregate = new(new SideEffectsAggregate());
    private readonly CancellationTokenSource _cts = new();
    private Task? _loop;

    public ISharedViewableProperty<SideEffectsAggregate> Aggregate => _aggregate;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _loop = Loop(_cts.Token);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _cts.Cancel();
        await _loop.ThrowIfNull().WaitAsync(cancellationToken);
        _cts.Dispose();
    }

    private async Task Loop(CancellationToken token)
    {
        while (token.IsCancellationRequested == false)
        {
            try
            {
                await CollectAndAggregate();
            }
            catch (Exception e)
            {
                // The top of the loop: the next pass tries again in 5 s.
                _logger.LogError(e, "[SideEffectsAggregator] Unexpected error in loop");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(5), token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    private async Task CollectAndAggregate()
    {
        var services = _discovery.Entries.Values.ToList();
        var perService = (await Task.WhenAll(services.Select(Collect))).ToList();

        _aggregate.Set(BuildAggregate(perService));
    }

    private async Task<PerServiceSideEffects> Collect(IServiceOverview service)
    {
        try
        {
            var pipeId = new MessagePipeServiceRequestId(service, typeof(SideEffectsSnapshotRequest));
            var response = await _messaging.SendPipe<SideEffectsSnapshotResponse>(pipeId, new SideEffectsSnapshotRequest());

            return new PerServiceSideEffects
            {
                ServiceTag = response.ServiceTag,
                ServiceId = response.ServiceId,
                Data = response.Data,
            };
        }
        catch (Exception e)
        {
            // A service that does not answer stays on the page as failed: its counts are unknown, not zero.
            _logger.LogError(e, "[SideEffectsAggregator] Failed to collect snapshot from {Service}", service.Tag);

            return new PerServiceSideEffects
            {
                ServiceTag = service.Tag.ToString(),
                ServiceId = service.Id,
                Failure = e.Message,
            };
        }
    }

    private static SideEffectsAggregate BuildAggregate(List<PerServiceSideEffects> services)
    {
        var answered = services.Where(service => service.Failure == null).ToList();

        // Every service counts the same shared tables (COUNT(*) of the queue, the retries, the dead letters): the counters
        // are the latest reading of one of them, summing them multiplied the counts by the number of services.
        var counts = answered.MaxBy(service => service.Data.ThroughputHistory.LastOrDefault()?.Timestamp ?? DateTime.MinValue)?.Data
            ?? new SideEffectsLiveData();

        var throughputBuckets = new Dictionary<DateTime, (long Processed, long Failed)>();

        foreach (var entry in answered.SelectMany(service => service.Data.ThroughputHistory))
        {
            var bucket = new DateTime(entry.Timestamp.Ticks - (entry.Timestamp.Ticks % TimeSpan.TicksPerSecond));
            var existing = throughputBuckets.GetValueOrDefault(bucket);
            throughputBuckets[bucket] = (existing.Processed + entry.Processed, existing.Failed + entry.Failed);
        }

        var aggregatedHistory = throughputBuckets
            .OrderBy(kv => kv.Key)
            .Select(kv => new SideEffectsThroughputEntry
            {
                Timestamp = kv.Key,
                Processed = kv.Value.Processed,
                Failed = kv.Value.Failed,
            })
            .ToList();

        var retryEntries = answered
            .SelectMany(s => s.Data.RetryEntries)
            .GroupBy(e => e.Id)
            .Select(g => g.First())
            .ToList();

        return new SideEffectsAggregate
        {
            QueueCount = counts.QueueCount,
            ProcessingCount = counts.ProcessingCount,
            RetryCount = counts.RetryCount,
            DeadLetterCount = counts.DeadLetterCount,
            Services = services,
            AggregatedHistory = aggregatedHistory,
            RetryEntries = retryEntries,
            UpdatedAt = DateTime.UtcNow,
            MissingServices = services.Count - answered.Count,
            HasCounts = answered.Count > 0,
        };
    }
}
