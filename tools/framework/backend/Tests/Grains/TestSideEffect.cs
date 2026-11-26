using Common;
using Infrastructure;
using Infrastructure.State;
using Newtonsoft.Json;
using Orleans.Concurrency;
using Shared;

namespace Framework.Tests.Grains;

[GenerateSerializer]
public class TestSideEffect : ISideEffect
{
    [Id(0)] public Guid TargetGrainId { get; init; }

    public async Task Execute(IOrleans orleans)
    {
        var grain = orleans.GetGrain<ITxTestGrain>(TargetGrainId);
        var result = await orleans.Transactions.Run(() => grain.Increment());

        if (!result.IsSuccess)
            throw new Exception("Side effect transaction failed");
    }
}
