namespace Infrastructure.State;

public interface IGrainStateTransactionParticipant
{
    object GetState();
    void OnTransactionSuccess();
    void OnTransactionFailure();
}

public class State<T> : IGrainStateTransactionParticipant where T : class, new()
{
    public State(
        IGrainStateStorage stateStorage,
        IGrainContext context,
        IStateSerializer serializer)
    {
        _stateStorage = stateStorage;
        _context = context;
        _serializer = serializer;
    }

    private readonly IGrainStateStorage _stateStorage;
    private readonly IGrainContext _context;
    private readonly IStateSerializer _serializer;

    private string? _rawValue;
    private T? _value;
    private Guid _currentTransactionId;

    public T Value => _value!;

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

        _currentTransactionId = TransactionContextProvider.Current.Id;
        _rawValue = await _stateStorage.ReadRaw<T>(_context.GrainId);
        _value = _serializer.Deserialize<T>(_rawValue);
    }

    public Task Write()
    {
        if (TransactionContextProvider.Current == null)
            return _stateStorage.Write(_context.GrainId, _value!);

        if (TransactionContextProvider.Current.Id != _currentTransactionId)
            throw new InvalidOperationException("Concurrent transactions are not supported.");

        var handler = (GrainTransactionHandler)_context.GetComponent<IGrainTransactionHandler>()!;
        handler.RecordStateChanged(this);
        
        return Task.CompletedTask;
    }

    public object GetState()
    {
        return _value!;
    }

    public void OnTransactionSuccess()
    {
        _currentTransactionId = Guid.Empty;
    }

    public void OnTransactionFailure()
    {
        _value = _serializer.Deserialize<T>(_rawValue!);
        _currentTransactionId = Guid.Empty;
    }
}