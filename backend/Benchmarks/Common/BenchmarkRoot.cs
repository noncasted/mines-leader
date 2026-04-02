using System.Diagnostics;
using System.Text.Json;
using Cluster.Discovery;
using Common.Extensions;
using Common.Reactive;
using Infrastructure;
using Microsoft.Extensions.Logging;

namespace Benchmarks;

public abstract class BenchmarkRoot<TPayload> : IClusterTest where TPayload : class, new()
{
    protected BenchmarkRoot(ClusterTestUtils utils)
    {
        _utils = utils;
        _payload = new TPayload();
    }

    private readonly ClusterTestUtils _utils;
    private TPayload _payload;

    object IClusterTest.Payload
    {
        get => _payload!;
        set => _payload = (TPayload)value;
    }

    BenchmarkResult? IClusterTest.LastResult { get; set; }

    Task IClusterTest.Start(IOperationProgress progress, CancellationToken cancellationToken) => Start(progress, _payload, cancellationToken);
    public abstract string Group { get; }
    public abstract string Title { get; }
    public abstract string MetricName { get; }

    public IMessaging Messaging => _utils.Messaging;
    public IServiceEnvironment Environment => _utils.Environment;
    public ILogger Logger => _utils.Logger;
    public ClusterTestUtils Utils => _utils;
    public TestCleanup Cleanup => _utils.Cleanup;

    public async Task Start(IOperationProgress progress, TPayload payload, CancellationToken cancellationToken = default)
    {
        var lifetime = new Lifetime();
        var handle = new BenchmarkNodeHandle(_utils, progress, lifetime, cancellationToken);
        progress.SetStatus(OperationStatus.Preparing);

        var stopwatch = Stopwatch.StartNew();
        var success = false;
        var cancelled = false;
        var errorMessage = string.Empty;

        try
        {
            await Run(handle, payload);
            success = !cancellationToken.IsCancellationRequested;
            cancelled = cancellationToken.IsCancellationRequested;

            if (cancelled)
                progress.SetStatus(OperationStatus.Cancelled);
            else
                progress.SetStatus(OperationStatus.Success);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            cancelled = true;
            progress.SetStatus(OperationStatus.Cancelled);
            Logger.LogInformation("Benchmark {TestName} was cancelled", Title);
        }
        catch (Exception e)
        {
            errorMessage = e.Message;
            progress.Log(e.Message);
            progress.SetStatus(OperationStatus.Failed);
            Logger.LogError(e, "Benchmark {TestName} failed with exception", Title);
        }

        stopwatch.Stop();

        var state = handle.Metrics.Collect();
        state.Name = Title;
        state.Success = success;

        if (state.Duration == TimeSpan.Zero)
            state.Duration = stopwatch.Elapsed;

        var metricValue = state.Duration.TotalSeconds > 0
            ? state.Records.Sum(r => r.Count) / state.Duration.TotalSeconds
            : stopwatch.ElapsedMilliseconds;

        var result = new BenchmarkResult
        {
            BenchmarkName = Title,
            Group = Group,
            MetricName = MetricName,
            MetricValue = metricValue,
            DurationMs = stopwatch.ElapsedMilliseconds,
            PayloadJson = SerializePayload(payload),
            Success = success,
            ErrorMessage = errorMessage
        };

        ((IClusterTest)this).LastResult = result;

        if (!cancelled)
        {
            try
            {
                await _utils.BenchmarkStorage.Write(state);
            }
            catch (Exception e)
            {
                Logger.LogError(e, "Benchmark {TestName} failed to save state", Title);
            }
        }

        try
        {
            await Cleanup.Execute();
        }
        catch (Exception e)
        {
            Logger.LogError(e, "Benchmark {TestName} cleanup failed", Title);
        }

        lifetime.Terminate();
        await handle.TerminateAllNodes();
    }

    protected abstract Task Run(BenchmarkNodeHandle handle, TPayload payload);

    private static string SerializePayload(TPayload payload)
    {
        try
        {
            return JsonSerializer.Serialize(payload);
        }
        catch
        {
            return "{}";
        }
    }
}
