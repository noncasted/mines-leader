namespace Infrastructure.State;

public interface IGrainEventTransactionParticipant
{
    string StreamId { get; }
    IStateValue? GetAggregate();
    IReadOnlyList<EventPayload> GetPendingEvents();
    void OnTransactionSuccess();
    void OnTransactionFailure();
}
