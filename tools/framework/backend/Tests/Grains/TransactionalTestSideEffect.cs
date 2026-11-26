using Common;
using Infrastructure;
using Infrastructure.State;
using Newtonsoft.Json;
using Orleans.Concurrency;
using Shared;

namespace Framework.Tests.Grains;

// Transactional side effect — executes inside transaction, deletion atomic with commit
[GenerateSerializer]
public class TransactionalTestSideEffect : ITransactionalSideEffect
{
    [Id(0)] public Guid TargetGrainId { get; init; }

    public async Task Execute(IOrleans orleans)
    {
        var grain = orleans.GetGrain<ITxTestGrain>(TargetGrainId);
        await grain.Increment();
    }
}
