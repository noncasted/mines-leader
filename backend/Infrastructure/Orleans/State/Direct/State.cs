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

    public T Value => _value.ThrowIfNull();

    public async Task Read()
    {
        if (TransactionContextProvider.Current == null)
        {
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
        _value = await _stateStorage.Read<T>(_context.GrainId);
        var handler = (GrainTransactionHandler)_context.GetComponent<IGrainTransactionHandler>().ThrowIfNull();
        handler.RecordStateChanged(this);
    }

    public Task Write()
    {
        if (TransactionContextProvider.Current == null)
            return _stateStorage.Write(_context.GrainId, _value!);

        if (TransactionContextProvider.Current.Id != _currentTransactionId)
            throw new InvalidOperationException("Concurrent transactions are not supported.");

        var handler = (GrainTransactionHandler)_context.GetComponent<IGrainTransactionHandler>().ThrowIfNull();
        handler.RecordStateChanged(this);

        return Task.CompletedTask;
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
}