namespace Infrastructure.State;

public interface IGrainStateTransactionParticipant
{
    IStateValue GetState();
    void OnTransactionSuccess();
    void OnTransactionFailure();
}
