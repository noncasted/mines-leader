namespace Infrastructure.Messaging;

public interface IMessageStreamObserver : IGrainObserver
{
    Task Send(object message);
    Task<TResponse> Send<TResponse>(object message);
}