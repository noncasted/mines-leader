namespace Infrastructure.Messaging;

public interface IMessageQueue : IGrainWithStringKey
{
    Task AddObserver(IMessageQueueObserver observer);
    Task PushDirect(IClusterMessage message);
    Task PushTransactional(IClusterMessage message);
}