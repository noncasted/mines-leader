using Infrastructure.State;
using Microsoft.Extensions.Logging;
using Orleans.Concurrency;

namespace Tests;

public interface ITransactionStateTestGrain : IGrainWithStringKey
{
    [Infrastructure.State.Transaction]
    Task Test();
}

[GenerateSerializer]
public class TransactionStateTestGrainState
{
    [Id(0)]
    public int Inc { get; set; }
}

public class TransactionStateTestGrain : Grain, ITransactionStateTestGrain
{
    public TransactionStateTestGrain(
        [State] State<TransactionStateTestGrainState> state,
        ILogger<TransactionStateTestGrain> logger)
    {
        _state = state;
        _logger = logger;
    }

    private readonly State<TransactionStateTestGrainState> _state;
    private readonly ILogger<TransactionStateTestGrain> _logger;

    public async Task Test()
    {
        await _state.Read();
        _state.Value.Inc++;
        await _state.Write();
        _logger.LogInformation("[Test] Inc: {ValueInc}", _state.Value.Inc);
    }
}