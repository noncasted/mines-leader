namespace Infrastructure.Messaging;

public interface IMessageQueueObserver : IGrainObserver
{
    Task Send(IReadOnlyList<IClusterMessage> messages);
}