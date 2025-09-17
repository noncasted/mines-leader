namespace Infrastructure.Messaging;

public interface IMessagingListener : IGrainObserver
{
    Task Ping();
    Task Send(IClusterMessage message);
    Task<TResponse> Send<TResponse>(IClusterMessage message);
}