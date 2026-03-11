using Orleans.Concurrency;

namespace Infrastructure.State;

public interface IGrainTransactionHandler : IGrainExtension
{
    [AlwaysInterleave]
    Task<Guid> Join(Guid transactionId);

    [AlwaysInterleave]
    Task<IReadOnlyList<object>> CollectStates(Guid transactionId);

    [AlwaysInterleave]
    Task OnSuccess(Guid transactionId);

    [AlwaysInterleave]
    Task OnFailure(Guid transactionId);
}

public class GrainTransactionHandler : IGrainTransactionHandler
{
    public GrainTransactionHandler(IGrainContext context)
    {
        _context = context;
    }

    private Guid _currentTransactionId;

    private readonly IGrainContext _context;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly Guid _participantId = Guid.NewGuid();
    private readonly HashSet<IGrainStateTransactionParticipant> _states = new();

    public async Task<Guid> Join(Guid transactionId)
    {
        if (_currentTransactionId == transactionId)
            return _participantId;

        if (_currentTransactionId == Guid.Empty)
        {
            _currentTransactionId = transactionId;
            return _participantId;
        }

        await _lock.WaitAsync(TimeSpan.FromSeconds(10f));

        if (_currentTransactionId != Guid.Empty && _currentTransactionId != transactionId)
        {
            throw new Exception(
                $"Handler failed to join transaction id '{transactionId}'. Current transaction in progress '{_currentTransactionId}'."
            );
        }

        return _participantId;
    }

    public void RecordStateChanged(IGrainStateTransactionParticipant state)
    {
        _states.Add(state);
    }

    public Task<IReadOnlyList<object>> CollectStates(Guid transactionId)
    {
        if (_currentTransactionId != transactionId)
        {
            throw new Exception(
                $"Handler failed to complete transaction id '{transactionId}'. Current transaction in progress '{_currentTransactionId}'."
            );
        }

        var states = new List<object>();

        foreach (var state in _states)
            states.Add(state.GetState());

        return Task.FromResult((IReadOnlyList<object>)states);
    }

    public Task OnSuccess(Guid transactionId)
    {
        if (_currentTransactionId != transactionId)
        {
            throw new Exception(
                $"Handler failed to complete transaction id '{transactionId}'. Current transaction in progress '{_currentTransactionId}'."
            );
        }

        foreach (var state in _states)
            state.OnTransactionSuccess();

        _states.Clear();
        _currentTransactionId = Guid.Empty;

        if (_lock.CurrentCount == 0)
            _lock.Release();

        return Task.CompletedTask;
    }

    public Task OnFailure(Guid transactionId)
    {
        if (_currentTransactionId != transactionId)
            return Task.CompletedTask;

        foreach (var state in _states)
            state.OnTransactionFailure();

        _states.Clear();
        _currentTransactionId = Guid.Empty;

        if (_lock.CurrentCount == 0)
            _lock.Release();
   
        return Task.CompletedTask;
    }
}