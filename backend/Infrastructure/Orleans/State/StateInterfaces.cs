namespace Infrastructure.State;

public interface IStateValue
{
    int Version { get; }
}

public interface IDirectStateValue : IStateValue { }

public interface IEventStateValue : IStateValue
{
    string Id { get; }
}

public interface IGrainStateTransactionParticipant
{
    IStateValue GetState();
    void OnTransactionSuccess();
    void OnTransactionFailure();
}
