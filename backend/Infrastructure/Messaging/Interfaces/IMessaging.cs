namespace Infrastructure.Messaging;

public interface IMessageQueueId
{
    string ToString();
}

public interface IMessaging
{
    Task PushQueue(IMessageQueueId id, IClusterMessage message);

    Task SendStream(IMessageOptions options, IClusterMessage message);

    Task<TResponse> SendStream<TResponse>(IMessageOptions options, IClusterMessage message)
        where TResponse : IClusterMessage;
}