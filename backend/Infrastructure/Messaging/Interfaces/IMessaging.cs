using Common;

namespace Infrastructure.Messaging;

public interface IMessageQueueId
{
    string ToString();
}

public interface IMessageStreamId
{
    string ToString();
}

public interface IMessaging
{
    Task PushTransactionalQueue(IMessageQueueId id, IClusterMessage message);
    Task PushDirectQueue(IMessageQueueId id, IClusterMessage message);
    void ListenQueue<T>(IReadOnlyLifetime lifetime, Action<T> listener) where T : IClusterMessage;

    Task SendStream(IMessageOptions options, IClusterMessage message);

    Task<TResponse> SendStream<TResponse>(IMessageOptions options, IClusterMessage message)
        where TResponse : IClusterMessage;
    
    IViewableDelegate<T> GetStreamDelegate<T>() where T : IClusterMessage;

    void ListenStream<TRequest, TResponse>(IReadOnlyLifetime lifetime, Func<TRequest, Task<TResponse>> listener)
        where TRequest : IClusterMessage
        where TResponse : IClusterMessage;

}