using Common.Extensions;

namespace Infrastructure.State;

public class State<T> : IGrainStateTransactionParticipant where T : class, IDirectStateValue, new()
{
    public State(IStateStorage stateStorage, IGrainContext context)
    {
        _stateStorage = stateStorage;
        _context = context;
    }

    private readonly IStateStorage _stateStorage;
    private readonly IGrainContext _context;

    private T? _value;
    private Guid _currentTransactionId;

    // In-flight transactional load. A reentrant grain can get two parallel calls in one transaction;
    // the second must wait for the first load instead of returning while _value is still null.
    private Task? _transactionLoad;

    private readonly SemaphoreSlim _directWriteLock = new(1, 1);

    public T Value => _value.ThrowIfNull();

    public async Task Read()
    {
        if (TransactionContextProvider.Current == null)
        {
            ThrowIfInTransaction();

            if (_value != null)
                return;

            _value = await _stateStorage.Read<T>(_context.GrainId);
            return;
        }

        if (_currentTransactionId != Guid.Empty && TransactionContextProvider.Current.Id != _currentTransactionId)
            throw new InvalidOperationException("Concurrent transactions are not supported.");

        if (_currentTransactionId == TransactionContextProvider.Current.Id)
        {
            if (_transactionLoad != null)
                await _transactionLoad;

            return;
        }

        _currentTransactionId = TransactionContextProvider.Current.Id;
        _transactionLoad = LoadInTransaction();

        try
        {
            await _transactionLoad;
        }
        finally
        {
            _transactionLoad = null;
        }
    }

    private async Task LoadInTransaction()
    {
        // Registered before the read: a failed read is rolled back by the handler (OnTransactionFailure), otherwise
        // the state keeps the id of the dead transaction and rejects every later one.
        var handler = (GrainTransactionHandler)_context.GetComponent<IGrainTransactionHandler>().ThrowIfNull();
        handler.RecordStateChanged(this);
        _value = await _stateStorage.Read<T>(_context.GrainId);
    }

    public Task Write()
    {
        if (TransactionContextProvider.Current == null)
        {
            ThrowIfInTransaction();
            return WriteDirect();
        }

        if (TransactionContextProvider.Current.Id != _currentTransactionId)
            throw new InvalidOperationException("Concurrent transactions are not supported.");

        var handler = (GrainTransactionHandler)_context.GetComponent<IGrainTransactionHandler>().ThrowIfNull();
        handler.RecordStateChanged(this);

        return Task.CompletedTask;
    }

    // Outside a transaction the change is already in memory. The writes go one at a time, each with the value of the
    // moment it starts: two writes of a reentrant grain in flight together could land in the database in the other
    // order and leave an older value there. A failed write drops the value: the next read reloads the database
    // instead of serving a change that is not stored.
    private async Task WriteDirect()
    {
        await _directWriteLock.WaitAsync();

        try
        {
            await _stateStorage.Write(_context.GrainId, _value.ThrowIfNull());
        }
        catch
        {
            _value = null;
            throw;
        }
        finally
        {
            _directWriteLock.Release();
        }
    }

    public IStateValue GetState()
    {
        return _value.ThrowIfNull();
    }

    public void OnTransactionSuccess()
    {
        _currentTransactionId = Guid.Empty;
    }

    public void OnTransactionFailure()
    {
        _value = null;
        _currentTransactionId = Guid.Empty;
    }

    // A call outside a transaction between the steps of an open one would see its uncommitted changes in memory, and a
    // direct write would store them even if the transaction then rolls back.
    private void ThrowIfInTransaction()
    {
        if (_currentTransactionId != Guid.Empty)
        {
            throw new InvalidOperationException(
                $"[State] {typeof(T).Name} of {_context.GrainId} is used outside a transaction while transaction " +
                $"{_currentTransactionId} holds it.");
        }
    }
}
