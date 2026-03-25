using Infrastructure;
using Infrastructure.State;

namespace Tests;

[GenerateSerializer]
public class TransactionTestState : IStateValue
{
    [Id(0)]
    public int Value { get; set; }

    public int Version => 0;
}

public interface ITransactionTestGrain : IGrainWithGuidKey
{
    [Transaction]
    Task Increment();

    Task<int> Get();
}

public class TransactionTestGrain : Grain, ITransactionTestGrain
{
    public TransactionTestGrain([State] State<TransactionTestState> state)
    {
        _state = state;
    }

    private readonly State<TransactionTestState> _state;

    public Task Increment()
    {
        return _state.Write(s =>
            {
                s.Value += 1;
            }
        );
    }

    public async Task<int> Get()
    {
        await _state.Read();
        return _state.Value.Value;
    }
}