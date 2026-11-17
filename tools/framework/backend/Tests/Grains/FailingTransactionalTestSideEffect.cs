using Common;
using Infrastructure;
using Infrastructure.State;
using Newtonsoft.Json;
using Orleans.Concurrency;
using Shared;

namespace Framework.Tests.Grains;

// Transactional side effect that fails N times before succeeding (uses shared FailingTestSideEffect counter)
[GenerateSerializer]
public class FailingTransactionalTestSideEffect : ITransactionalSideEffect
{
    [Id(0)] public Guid TargetGrainId { get; init; }
    [Id(1)] public int FailCount { get; init; }

    public async Task Execute(IOrleans orleans)
    {
        lock (FailingTestSideEffect.AttemptsLock)
        {
            FailingTestSideEffect.AttemptsDict.TryGetValue(TargetGrainId, out var count);
            FailingTestSideEffect.AttemptsDict[TargetGrainId] = count + 1;

            if (count < FailCount)
                throw new Exception($"Intentional transactional failure {count + 1}/{FailCount}");
        }

        var grain = orleans.GetGrain<ITxTestGrain>(TargetGrainId);
        await grain.Increment();
    }
}
