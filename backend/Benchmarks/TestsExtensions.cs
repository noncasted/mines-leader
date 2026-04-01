using System.Diagnostics;

namespace Benchmarks;

public static class TestsExtensions
{
    public static Task RunConcurrentIterations(
        this ClusterTestNodeHandle handle,
        IConcurrentIterationTestPayload payload,
        Func<Task> action)
    {
        return handle.RunConcurrentIterations(payload.Iterations, payload.Concurrent, action);
    }

    public static async Task RunConcurrentIterations(
        this ClusterTestNodeHandle handle,
        int iterations,
        int concurrent,
        Func<Task> action)
    {
        var stopwatch = Stopwatch.StartNew();
        var snapshotInterval = Math.Max(1, iterations / 20); // ~5% steps

        for (var i = 0; i < iterations; i++)
        {
            var tasks = new List<Task>();

            for (var c = 0; c < concurrent; c++)
                tasks.Add(action());

            await Task.WhenAll(tasks);

            var progress = (float)(i + 1) / iterations;
            handle.Progress.SetProgress(progress);
            handle.Progress.Log($"Processed {i + 1}/{iterations}");

            if ((i + 1) % snapshotInterval == 0 || i == iterations - 1)
            {
                var elapsed = stopwatch.Elapsed.TotalSeconds;
                var totalOps = (i + 1) * concurrent;
                var currentOpsPerSecond = elapsed > 0 ? totalOps / elapsed : 0;
                handle.RecordSnapshot(progress, currentOpsPerSecond);
            }
        }

        stopwatch.Stop();
        var totalOpsAll = iterations * concurrent;
        var opsPerSecond = totalOpsAll / stopwatch.Elapsed.TotalSeconds;
        handle.ReportMetric(opsPerSecond);
    }
}
