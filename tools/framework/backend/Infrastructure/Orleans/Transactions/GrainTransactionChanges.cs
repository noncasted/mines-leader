using Infrastructure.State;

namespace Infrastructure;

// What the transaction holding a grain changed in it: told to confirm or to roll back when the transaction ends.
internal class GrainTransactionChanges
{
    private readonly HashSet<IGrainStateTransactionParticipant> _states = new();
    private readonly HashSet<IGrainEventTransactionParticipant> _events = new();
    private readonly List<GrainEventRecord> _eventRecords = [];

    private long _resultSequence;

    public void Record(IGrainStateTransactionParticipant state)
    {
        _states.Add(state);
    }

    public void Record(IGrainEventTransactionParticipant events)
    {
        _events.Add(events);
    }

    public void Record(IGrainEventTransactionParticipant events, IReadOnlyList<EventPayload> payloads)
    {
        _events.Add(events);
        _eventRecords.Add(new GrainEventRecord
        {
            StreamId = events.StreamId,
            Events = payloads.ToList()
        });
    }

    public TransactionHandlerResult Snapshot()
    {
        var states = new List<IStateValue>();

        foreach (var state in _states)
            states.Add(state.GetState());

        return new TransactionHandlerResult
        {
            States = states,
            Events = _eventRecords.ToList(),
            Sequence = ++_resultSequence
        };
    }

    // Everyone is told and the changes are forgotten even when some of them throw; returns the failures, if any.
    public List<Exception>? Confirm()
    {
        List<Exception>? failures = null;

        foreach (var state in _states)
            ConfirmOne(state.OnTransactionSuccess);

        foreach (var ev in _events)
            ConfirmOne(ev.OnTransactionSuccess);

        Clear();

        return failures;

        void ConfirmOne(Action onSuccess)
        {
            try
            {
                onSuccess();
            }
            catch (Exception e)
            {
                failures ??= [];
                failures.Add(e);
            }
        }
    }

    // Everyone is told even when some of them throw; returns the failures, if any. The ones that threw stay here for the
    // next attempt: their memory is unknown until a rollback of theirs succeeds.
    public List<Exception>? Rollback()
    {
        List<Exception>? failures = null;

        _states.RemoveWhere(state => RollbackOne(state.OnTransactionFailure));
        _events.RemoveWhere(ev => RollbackOne(ev.OnTransactionFailure));
        _eventRecords.Clear();

        return failures;

        bool RollbackOne(Action onFailure)
        {
            try
            {
                onFailure();
                return true;
            }
            catch (Exception e)
            {
                failures ??= [];
                failures.Add(e);
                return false;
            }
        }
    }

    private void Clear()
    {
        _states.Clear();
        _events.Clear();
        _eventRecords.Clear();
    }
}
