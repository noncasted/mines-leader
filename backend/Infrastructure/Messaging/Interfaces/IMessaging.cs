using Common;

namespace Infrastructure.Messaging;

public interface IMessageQueueId
{
    string ToString();
}

public class MessageQueueId : IMessageQueueId
{
    public MessageQueueId(string id)
    {
        _id = id;
    }

    private readonly string _id;

    public override string ToString()
    {
        return _id;
    }
}

public interface IMessageStreamId
{
    string ToString();
}

public interface IMessaging
{
    IMessageQueueClient Queue { get; }

    Task SendStream(IMessageOptions options, IClusterMessage message);

    Task<TResponse> SendStream<TResponse>(IMessageOptions options, IClusterMessage message)
        where TResponse : IClusterMessage;
    
    IViewableDelegate<T> GetStreamDelegate<T>() where T : IClusterMessage;

    void ListenStream<TRequest, TResponse>(IReadOnlyLifetime lifetime, Func<TRequest, Task<TResponse>> listener)
        where TRequest : IClusterMessage
        where TResponse : IClusterMessage;
}

public static class MessagingExtensions
{
    public static Task PushTransactionalQueue(this IMessaging messaging, IMessageQueueId id, object message)
    {
        return messaging.Queue.PushTransactional(id, message);
    }

    public static Task PushDirectQueue(this IMessaging messaging, IMessageQueueId id, object message)
    {
        return messaging.Queue.PushDirect(id, message);
    }

    public static void ListenQueue<T>(
        this IMessaging messaging,
        IReadOnlyLifetime lifetime,
        IMessageQueueId id,
        Action<T> listener)
    {
        var consumer = messaging.Queue.GetOrCreateConsumer<T>(id);
        consumer.Advise(lifetime, listener);
    }
}