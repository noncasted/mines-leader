using System.Diagnostics;
using Infrastructure;

namespace Framework.Tests.Fixtures;

public record DrainResult(IReadOnlyList<BatchExecutionInfo> ExecutionTrace, bool ReachedQuiescence)
{
    public static DrainResult Quiet { get; } = new(Array.Empty<BatchExecutionInfo>(), true);

    public int TotalTasks => ExecutionTrace.Count;
    public bool AllSucceeded => ExecutionTrace.All(e => e.Success);
}

public static class DrainResultExtensions
{
    public static void AssertDrainedSuccessfully(this DrainResult result)
    {
        if (!result.ReachedQuiescence)
            throw new Exception("Drain did not reach quiescence");

        if (!result.AllSucceeded)
            throw new Exception($"Drain had failures: {FormatTrace(result)}");
    }

    public static void AssertDrainedWithWork(this DrainResult result)
    {
        result.AssertDrainedSuccessfully();

        if (result.TotalTasks == 0)
            throw new Exception("Drain completed but no work was executed");
    }

    private static string FormatTrace(DrainResult result)
    {
        var failures = result.ExecutionTrace.Where(e => !e.Success).ToList();
        return string.Join("\n", failures.Select(f => $"  [{f.Id}] {f.ShortError}"));
    }
}
