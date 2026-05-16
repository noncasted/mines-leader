using Cluster.Coordination;
using Cluster.Discovery;
using Cluster.Monitoring;
using Common.Reactive;
using Infrastructure;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Console;

public interface ISideEffectsAggregator
{
    IViewableProperty<SideEffectsAggregate> Aggregate { get; }
}

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

    private readonly ViewableProperty<SideEffectsAggregate> _aggregate = new(new SideEffectsAggregate());
    private CancellationTokenSource? _cts;

    public IViewableProperty<SideEffectsAggregate> Aggregate => _aggregate;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        Loop(_cts.Token);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _cts?.Cancel();
        return Task.CompletedTask;
    }

    private async void Loop(CancellationToken token)
    {
        while (token.IsCancellationRequested == false)
        {
            try
            {
                await CollectAndAggregate();
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "[SideEffectsAggregator] Unexpected error in loop");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(5), token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task CollectAndAggregate()
    {
        var services = _discovery.Entries.Values.ToList();

        var tasks = services.Select(async svc => {
            try
            {
                var pipeId = new MessagePipeServiceRequestId(svc, typeof(SideEffectsSnapshotRequest));
                return await _messaging.SendPipe<SideEffectsSnapshotResponse>(pipeId, new SideEffectsSnapshotRequest());
            }
            catch (Exception e)
            {
                _logger.LogError(e, "[SideEffectsAggregator] Failed to collect snapshot from {Service}", svc.Tag);
                return null;
            }
        }).ToList();

        var responses = await Task.WhenAll(tasks);

        var perService = responses
            .Where(r => r != null)
            .Select(r => new PerServiceSideEffects {
                ServiceTag = r!.ServiceTag,
                ServiceId = r.ServiceId,
                Data = r.Data,
            })
            .ToList();

        _aggregate.Set(BuildAggregate(perService));
    }

    private static SideEffectsAggregate BuildAggregate(List<PerServiceSideEffects> services)
    {
        var queueCount = 0;
        var processingCount = 0;
        var retryCount = 0;
        var deadLetterCount = 0;

        var throughputBuckets = new Dictionary<DateTime, (long Processed, long Failed)>();

        foreach (var svc in services)
        {
            queueCount += svc.Data.QueueCount;
            processingCount += svc.Data.ProcessingCount;
            retryCount += svc.Data.RetryCount;
            deadLetterCount += svc.Data.DeadLetterCount;

            foreach (var entry in svc.Data.ThroughputHistory)
            {
                var bucket = new DateTime(entry.Timestamp.Ticks - (entry.Timestamp.Ticks % TimeSpan.TicksPerSecond));

                if (throughputBuckets.TryGetValue(bucket, out var existing))
                    throughputBuckets[bucket] = (existing.Processed + entry.Processed, existing.Failed + entry.Failed);
                else
                    throughputBuckets[bucket] = (entry.Processed, entry.Failed);
            }
        }

        var aggregatedHistory = throughputBuckets
            .OrderBy(kv => kv.Key)
            .Select(kv => new SideEffectsThroughputEntry {
                Timestamp = kv.Key,
                Processed = kv.Value.Processed,
                Failed = kv.Value.Failed,
            })
            .ToList();

        var retryEntries = services
            .SelectMany(s => s.Data.RetryEntries)
            .GroupBy(e => e.Id)
            .Select(g => g.First())
            .ToList();

        return new SideEffectsAggregate {
            QueueCount = queueCount,
            ProcessingCount = processingCount,
            RetryCount = retryCount,
            DeadLetterCount = deadLetterCount,
            Services = services,
            AggregatedHistory = aggregatedHistory,
            RetryEntries = retryEntries,
            UpdatedAt = DateTime.UtcNow,
        };
    }
}
