namespace Infrastructure.Messaging;

public interface IMessageStreamObserver : IGrainObserver
{
    Task Send(IClusterMessage message);
    Task<TResponse> Send<TResponse>(IClusterMessage message);
}