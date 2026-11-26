using Infrastructure;
using Infrastructure.State;
using Orleans.Concurrency;

namespace Framework.Tests.Grains;

// A [Transaction] method of every return type the attribute maps to its own request class, in every way a method ends.
public interface ITxRequestKindsGrain : IGrainWithGuidKey
{
    [Transaction]
    ValueTask ValueTaskVoid(TxRequestMode mode);

    [Transaction]
    ValueTask<int> ValueTaskResult(TxRequestMode mode);

    [Transaction]
    Task TaskVoid(TxRequestMode mode);

    [Transaction]
    Task<int> TaskResult(TxRequestMode mode);

    // Writes the state and then throws: the action may catch it and go on to the commit.
    [Transaction]
    Task IncrementThenThrow();

    Task<int> Get();
    Task<Guid> GetActivationId();
    Task Deactivate();
}

[Reentrant]
public class TxRequestKindsGrain : Grain, ITxRequestKindsGrain
{
    public TxRequestKindsGrain([State] State<TxTestState> state)
    {
        _state = state;
    }

    public const int Result = 42;
    public const string Failure = "request kind failed";

    private readonly State<TxTestState> _state;
    private readonly Guid _activationId = Guid.NewGuid();

    public ValueTask ValueTaskVoid(TxRequestMode mode)
    {
        return mode switch {
            TxRequestMode.Completed => ValueTask.CompletedTask,
            TxRequestMode.Faulted => ValueTask.FromException(new InvalidOperationException(Failure)),
            TxRequestMode.ThrowsAtCall => throw new InvalidOperationException(Failure),
            _ => new ValueTask(Run(mode))
        };
    }

    public ValueTask<int> ValueTaskResult(TxRequestMode mode)
    {
        return mode switch {
            TxRequestMode.Completed => new ValueTask<int>(Result),
            TxRequestMode.Faulted => ValueTask.FromException<int>(new InvalidOperationException(Failure)),
            TxRequestMode.ThrowsAtCall => throw new InvalidOperationException(Failure),
            _ => new ValueTask<int>(Run(mode))
        };
    }

    public Task TaskVoid(TxRequestMode mode)
    {
        return mode switch {
            TxRequestMode.Completed => Task.CompletedTask,
            TxRequestMode.Faulted => Task.FromException(new InvalidOperationException(Failure)),
            TxRequestMode.ThrowsAtCall => throw new InvalidOperationException(Failure),
            _ => Run(mode)
        };
    }

    public Task<int> TaskResult(TxRequestMode mode)
    {
        return mode switch {
            TxRequestMode.Completed => Task.FromResult(Result),
            TxRequestMode.Faulted => Task.FromException<int>(new InvalidOperationException(Failure)),
            TxRequestMode.ThrowsAtCall => throw new InvalidOperationException(Failure),
            _ => Run(mode)
        };
    }

    public async Task IncrementThenThrow()
    {
        await _state.Write(s => s.Increment());
        throw new InvalidOperationException(Failure);
    }

    public Task<int> Get()
    {
        return _state.Read(s => s.Value);
    }

    public Task<Guid> GetActivationId()
    {
        return Task.FromResult(_activationId);
    }

    public Task Deactivate()
    {
        DeactivateOnIdle();
        return Task.CompletedTask;
    }

    private static async Task<int> Run(TxRequestMode mode)
    {
        await Task.Delay(1);

        if (mode == TxRequestMode.ThrowsAfterDelay)
            throw new InvalidOperationException(Failure);

        return Result;
    }
}
