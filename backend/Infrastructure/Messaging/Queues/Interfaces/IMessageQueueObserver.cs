namespace Infrastructure.Messaging;

public interface IMessageQueueObserver : IGrainObserver
{
    Task Send(IClusterMessage message);
}