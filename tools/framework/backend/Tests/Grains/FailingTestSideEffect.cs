using Common;
using Infrastructure;
using Infrastructure.State;
using Newtonsoft.Json;
using Orleans.Concurrency;
using Shared;

namespace Framework.Tests.Grains;

// Side effect that fails N times before succeeding
[GenerateSerializer]
public class FailingTestSideEffect : ISideEffect
{
    [Id(0)] public Guid TargetGrainId { get; init; }
    [Id(1)] public int FailCount { get; init; }

    // Static counter shared across executions (keyed by TargetGrainId to avoid cross-test interference)
    internal static readonly object AttemptsLock = new();
    internal static readonly Dictionary<Guid, int> AttemptsDict = new();

    public async Task Execute(IOrleans orleans)
    {
        lock (AttemptsLock)
        {
            AttemptsDict.TryGetValue(TargetGrainId, out var count);
            AttemptsDict[TargetGrainId] = count + 1;

            if (count < FailCount)
                throw new Exception($"Intentional failure {count + 1}/{FailCount}");
        }

        var grain = orleans.GetGrain<ITxTestGrain>(TargetGrainId);
        var result = await orleans.Transactions.Run(() => grain.Increment());

        if (!result.IsSuccess)
            throw new Exception("Side effect transaction failed");
    }

    public static void ResetAttempts()
    {
        lock (AttemptsLock)
        {
            AttemptsDict.Clear();
        }
    }

    public static int GetAttemptCount(Guid targetId)
    {
        lock (AttemptsLock)
        {
            return AttemptsDict.GetValueOrDefault(targetId);
        }
    }
}
