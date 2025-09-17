using Common;

namespace Infrastructure.Messaging;

public interface IMessageStreamClient
{
    Task Send(IMessageOptions options, object message);
    Task<TResponse> Send<TResponse>(IMessageOptions options, object message);
    IViewableDelegate<T> GetOrCreateConsumer<T>(IMessageStreamId id);
    void AddHandler<TRequest, TResponse>(
        IReadOnlyLifetime lifetime, 
        IMessageStreamId id,
        Func<TRequest, Task<TResponse>> listener);
}