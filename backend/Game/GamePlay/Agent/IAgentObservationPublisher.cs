namespace Game.GamePlay;

public interface IAgentObservationPublisher
{
    void Publish(Guid viewerId, string trigger, bool hasError, string error);
    void Publish(Guid viewerId, string trigger, bool hasError, string error, bool oracle);
}
