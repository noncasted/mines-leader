using Common;

namespace Infrastructure.Messaging;

public interface IMessagePipeClient
{
    Task Send(IMessagePipeId id, object message);
    Task<TResponse> Send<TResponse>(IMessagePipeId id, object message);
    IViewableDelegate<T> GetOrCreateConsumer<T>(IMessagePipeId id);
    void AddHandler<TRequest, TResponse>(
        IReadOnlyLifetime lifetime, 
        IMessagePipeId id,
        Func<TRequest, Task<TResponse>> listener);
}