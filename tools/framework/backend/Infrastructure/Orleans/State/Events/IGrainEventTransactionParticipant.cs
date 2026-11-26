namespace Infrastructure.State;

public interface IGrainEventTransactionParticipant
{
    string StreamId { get; }
    void OnTransactionSuccess();
    void OnTransactionFailure();
}
