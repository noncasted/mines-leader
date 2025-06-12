namespace Infrastructure.Messaging;

public interface IMessagingObserver : IGrainObserver
{
    Task Ping();
    Task Send(IClusterMessage message);
    Task<TResponse> Send<TResponse>(IClusterMessage message);
}