using System.Diagnostics;
using System.Text.Json;
using Cluster.Discovery;
using Common.Extensions;
using Common.Reactive;
using Infrastructure;
using Microsoft.Extensions.Logging;

namespace Benchmarks;

public abstract class ClusterTestRoot<TPayload> : IClusterTest where TPayload : class, new()
{
    protected ClusterTestRoot(ClusterTestUtils utils, BenchmarkStorage benchmarkStorage)
    {
        _utils = utils;
        _benchmarkStorage = benchmarkStorage;
        _payload = new TPayload();
    }

    private readonly ClusterTestUtils _utils;
    private readonly BenchmarkStorage _benchmarkStorage;
    private TPayload _payload;
    private double _reportedMetric;

    object IClusterTest.Payload
    {
        get => _payload!;
        set => _payload = (TPayload)value;
    }

    BenchmarkResult? IClusterTest.LastResult { get; set; }

    Task IClusterTest.Start(IOperationProgress progress) => Start(progress, _payload);
    public abstract string Group { get; }
    public abstract string Title { get; }
    public abstract string MetricName { get; }

    public IMessaging Messaging => _utils.Messaging;
    public IServiceEnvironment Environment => _utils.Environment;
    public ILogger Logger => _utils.Logger;
    public ClusterTestUtils Utils => _utils;
    public TestCleanup Cleanup => _utils.Cleanup;

    protected void ReportMetric(double value)
    {
        _reportedMetric = value;
    }

    public async Task Start(IOperationProgress progress, TPayload payload)
    {
        _reportedMetric = 0;
        var lifetime = new Lifetime();
        var handle = new ClusterTestNodeHandle(_utils, progress, lifetime, ReportMetric);
        progress.SetStatus(OperationStatus.Preparing);

        var stopwatch = Stopwatch.StartNew();
        var success = false;
        var errorMessage = string.Empty;

        try
        {
            await Run(handle, payload);
            success = true;
            progress.SetStatus(OperationStatus.Success);
        }
        catch (Exception e)
        {
            errorMessage = e.Message;
            progress.SetStatus(OperationStatus.Failed);
            Logger.LogError(e, "Benchmark {TestName} failed with exception", Title);
        }

        stopwatch.Stop();

        if (_reportedMetric == 0)
            _reportedMetric = stopwatch.ElapsedMilliseconds;

        var result = new BenchmarkResult
        {
            BenchmarkName = Title,
            Group = Group,
            MetricName = MetricName,
            MetricValue = _reportedMetric,
            DurationMs = stopwatch.ElapsedMilliseconds,
            PayloadJson = SerializePayload(payload),
            Success = success,
            ErrorMessage = errorMessage
        };

        ((IClusterTest)this).LastResult = result;
        await _benchmarkStorage.Save(result);

        if (handle.Snapshots.Count > 0)
            await _benchmarkStorage.SaveSnapshots(result.Id, handle.Snapshots);

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

    protected abstract Task Run(ClusterTestNodeHandle handle, TPayload payload);

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
