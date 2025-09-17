namespace Infrastructure.Messaging;

public interface IMessageQueue : IGrainWithStringKey
{
    Task AddObserver(IMessageQueueObserver observer);
    Task PushDirect(object message);
    Task PushTransactional(object message);
}