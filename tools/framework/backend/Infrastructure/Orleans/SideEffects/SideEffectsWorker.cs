using System.Collections.Concurrent;
using Common;
using Common.Extensions;
using Internal;
using Infrastructure.Startup;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure;

public class SideEffectsWorker : IHostedService
{
    public SideEffectsWorker(
        ISideEffectsStorage storage,
        ITransactions transactions,
        IOrleans orleans,
        IServiceLoopObserver loopObserver,
        ISideEffectsConfig config,
        IClusterFlags clusterFlags,
        IClusterParticipantContext participantContext,
        ILogger<SideEffectsWorker> logger)
    {
        _storage = storage;
        _execution = new SideEffectExecution(storage, transactions, orleans, config, logger);
        _loopObserver = loopObserver;
        _config = config;
        _clusterFlags = clusterFlags;
        _participantContext = participantContext;
        _logger = logger;
    }

    private readonly ISideEffectsStorage _storage;
    private readonly SideEffectExecution _execution;
    private readonly IServiceLoopObserver _loopObserver;
    private readonly ISideEffectsConfig _config;
    private readonly IClusterFlags _clusterFlags;
    private readonly IClusterParticipantContext _participantContext;
    private readonly ILogger<SideEffectsWorker> _logger;

    private int _inProgress;
    private readonly CancellationTokenSource _shutdownCts = new();
    private DateTime _lastStuckCheck = DateTime.MinValue;
    private Task _loop = Task.CompletedTask;

    // Entries this worker executes: touched before every stuck check, so a long effect is not taken for stuck.
    private readonly ConcurrentDictionary<Guid, byte> _executing = new();

    // The loop ends with StopAsync. The token of StartAsync only cancels the start, it is never cancelled at shutdown.
    public Task StartAsync(CancellationToken cancellationToken)
    {
        var lifetime = _shutdownCts.Token.ToLifetime();
        _loop = Loop(lifetime);
        _loop.NoAwait();
        return Task.CompletedTask;
    }

    private async Task Loop(IReadOnlyLifetime lifetime)
    {
        try
        {
            await _loopObserver.IsOrleansStarted.WaitTrue(lifetime);
            await _participantContext.IsInitialized.WaitTrue(lifetime);

            while (lifetime.IsTerminated == false)
                await Iterate(lifetime);
        }
        catch (OperationCanceledException) when (lifetime.IsTerminated == true)
        {
            // The stop of the worker: the loop just ends, StopAsync waits for the effects in progress.
        }
    }

    private async Task Iterate(IReadOnlyLifetime lifetime)
    {
        if (_clusterFlags.SideEffectsEnabled == false)
        {
            await Task.Delay(500, lifetime.Token);
            return;
        }

        var foundWork = false;

        try
        {
            var options = _config.Value;

            // Before the check of the slots: a worker with every slot busy runs the longest effects, and they must
            // stay touched.
            if ((DateTime.UtcNow - _lastStuckCheck).TotalSeconds >= options.StuckCheckIntervalSeconds)
            {
                // Every worker touches its own entries at its stuck check: only the entries of a worker that is
                // gone stay untouched for StuckThresholdMinutes.
                await _storage.TouchProcessing(_executing.Keys.ToArray());
                await _storage.RequeueStuckOlderThan(TimeSpan.FromMinutes(options.StuckThresholdMinutes));
                _lastStuckCheck = DateTime.UtcNow;
            }

            var freeSlots = options.ConcurrentExecutions - _inProgress;

            if (freeSlots <= 0)
            {
                await Task.Delay(options.EmptyScanDelay, lifetime.Token);
                return;
            }

            await _storage.RequeueReady();

            var entries = await _storage.Read(freeSlots);
            foundWork = entries.Count > 0;
            SideEffectTelemetry.QueueDepth.Record(entries.Count);

            // The read moved the entries to processing: they run even when the stop came meanwhile, otherwise they
            // hang there until the stuck check of another worker. StopAsync waits for them.
            foreach (var entry in entries)
                ExecuteEntry(entry).NoAwait();
        }
        catch (OperationCanceledException) when (lifetime.IsTerminated == true)
        {
            throw;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[SideEffects] Error in scan loop");
        }

        var delay = foundWork ? _config.Value.ScanDelay : _config.Value.EmptyScanDelay;
        await Task.Delay(delay, lifetime.Token);
    }

    private async Task ExecuteEntry(SideEffectEntry entry)
    {
        using var activity = SideEffectTelemetry.Source.StartActivity("SideEffect.Execute");
        activity?.SetTag("side_effect.retry_count", entry.RetryCount);

        if (entry.Effect is ICorrelatedSideEffect correlated)
            activity?.SetTag("side_effect.correlation_id", correlated.CorrelationId);

        Interlocked.Increment(ref _inProgress);
        _executing[entry.Id] = 0;
        SideEffectTelemetry.InProgress.Add(1);
        using var watch = MetricWatch.Start(SideEffectTelemetry.Duration);

        try
        {
            await _execution.Execute(entry);
        }
        finally
        {
            _executing.TryRemove(entry.Id, out _);
            SideEffectTelemetry.InProgress.Add(-1);
            Interlocked.Decrement(ref _inProgress);
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _shutdownCts.Cancel();
        await _loop.WaitAsync(cancellationToken);

        const int maxIterations = 200;
        var iterations = 0;

        while (Volatile.Read(ref _inProgress) > 0 && !cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(50, cancellationToken);
            iterations++;

            if (iterations >= maxIterations)
            {
                _logger.LogWarning("[SideEffects] Shutdown timeout exceeded, {InProgress} effects still in progress",
                    _inProgress);
                break;
            }
        }

        _shutdownCts.Dispose();
    }
}
