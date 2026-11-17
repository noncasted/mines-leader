using System.Diagnostics;
using Infrastructure;

namespace Framework.Tests.Fixtures;

public record PumpResult(IReadOnlyList<BatchExecutionInfo> Batches)
{
    public static PumpResult Empty { get; } = new(Array.Empty<BatchExecutionInfo>());

    public bool IsEmpty => Batches.Count == 0;
    public int TotalTasks => Batches.Count;
    public bool AllSucceeded => Batches.All(b => b.Success);
}
