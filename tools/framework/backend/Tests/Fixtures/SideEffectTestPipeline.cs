using System.Diagnostics;
using Infrastructure;

namespace Framework.Tests.Fixtures;

// Runs the side effects in the test instead of SideEffectsWorker: one pump, or a drain until the queue is quiet.
public class SideEffectTestPipeline
{
    public SideEffectTestPipeline(ISideEffectsStorage storage, SideEffectExecution execution, ISideEffectsConfig config)
    {
        _storage = storage;
        _execution = execution;
        _config = config;
    }

    private readonly ISideEffectsStorage _storage;
    private readonly SideEffectExecution _execution;
    private readonly ISideEffectsConfig _config;

    public async Task<PumpResult> PumpOnce()
    {
        await _storage.RequeueReady();

        var entries = await _storage.Read(_config.Value.ConcurrentExecutions);

        if (entries.Count == 0)
            return PumpResult.Empty;

        var results = new List<BatchExecutionInfo>();

        foreach (var entry in entries)
        {
            var info = await ExecuteEntry(entry);
            results.Add(info);
        }

        return new PumpResult(results);
    }

    private async Task<BatchExecutionInfo> ExecuteEntry(SideEffectEntry entry)
    {
        var watch = Stopwatch.StartNew();
        var error = await _execution.Execute(entry);
        return new BatchExecutionInfo(entry.Id, error == null, watch.Elapsed, error?.Message);
    }

    // Waits for the first entry to appear in the queue, then drains until quiet.
    public async Task<DrainResult> SettleAndDrain(int settleTimeoutMs = 500, int maxIterations = 50)
    {
        var settleStart = DateTime.UtcNow;

        while ((DateTime.UtcNow - settleStart).TotalMilliseconds < settleTimeoutMs)
        {
            await _storage.RequeueReady();
            var peek = await _storage.Read(1);

            if (peek.Count > 0)
            {
                // Read moved the entry to processing, so it is executed here and not by the drain.
                var firstResult = await ExecuteEntry(peek[0]);
                return await DrainUntilQuiet(maxIterations, [firstResult]);
            }

            await Task.Delay(10);
        }

        return DrainResult.Quiet;
    }

    public async Task<DrainResult> DrainUntilQuiet(
        int maxIterations = 50,
        List<BatchExecutionInfo>? initial = null)
    {
        var allResults = initial ?? [];
        var iterations = 0;

        while (iterations < maxIterations)
        {
            var result = await PumpOnce();

            if (result.IsEmpty)
                break;

            allResults.AddRange(result.Batches);
            iterations++;
        }

        return new DrainResult(allResults, iterations < maxIterations);
    }
}
