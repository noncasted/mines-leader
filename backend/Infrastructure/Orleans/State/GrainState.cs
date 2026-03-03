using Npgsql;
using Orleans.Serialization;
using Orleans.Transactions;

namespace Infrastructure.State;

public class GrainState<T> where T : class, new()
{
    public GrainState(
        IGrainStateStorage stateStorage,
        IGrainContext context,
        OrleansJsonSerializer serializer)
    {
        _stateStorage = stateStorage;
        _context = context;
        _serializer = serializer;
    }

    private readonly IGrainStateStorage _stateStorage;
    private readonly IGrainContext _context;
    private readonly OrleansJsonSerializer _serializer;

    private readonly SemaphoreSlim _lock = new(1, 1);

    private string? _rawValue;
    private T? _value;

    public async Task<DirectGrainStateHandle<T>> Handle()
    {
        await _lock.WaitAsync();
        
        if (TransactionContext.GetTransactionInfo() != null)
        {
            var transactionInfo = TransactionContext.GetTransactionInfo();
        }
        else
        {
            if (_value != null)
                return new DirectGrainStateHandle<T>(_value, this);
        }
        
        return new DirectGrainStateHandle<T>(_value, this);
    }

    public async Task Commit(T value)
    {
        _lock.Release();
    }
}

public interface IGrainStateHandle<T>
{
    T Value { get; }

    Task Commit();
}

public class DirectGrainStateHandle<T> where T : class, new()
{
    public DirectGrainStateHandle(T value, GrainState<T> state)
    {
        _value = value;
        _state = state;
    }

    private readonly T _value;
    private readonly GrainState<T> _state;

    public T Value => _value;

    public Task Commit()
    {
        return _state.Commit(_value);
    }
}

public class TransactionalGrainStateHandle<T> where T : class, new()
{
    public TransactionalGrainStateHandle(T value, GrainState<T> state)
    {
        _value = value;
        _state = state;
    }

    private readonly T _value;
    private readonly GrainState<T> _state;

    public T Value => _value;

    public Task Commit()
    {
        return _state.Commit(_value);
    }

    public Task Rollback()
    {
        return Task.CompletedTask;
    }
}