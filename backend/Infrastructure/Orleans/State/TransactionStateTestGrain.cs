using Infrastructure.State;
using Orleans.Concurrency;

namespace Tests;

public interface ITransactionStateTestGrain : IGrainWithStringKey
{
    [Infrastructure.State.Transaction]
    Task Test();

    Task A();
}

[GenerateSerializer]
public class TransactionStateTestGrainState
{
    [Id(0)]
    public int Inc { get; set; }
}



[Reentrant]
public class TransactionStateTestGrain : Grain, ITransactionStateTestGrain
{
    public async Task Test()
    {

        await GrainContext.GetComponent<IGrainTransactionHandler>().Test();
    }

    public Task A()
    {
        return Task.CompletedTask;
    }
}