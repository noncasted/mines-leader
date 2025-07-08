using Common;

namespace Infrastructure.Messaging;

public interface IMessagingClient
{
    Task Send(IMessageOptions options, IClusterMessage message);

    Task<TResponse> Send<TResponse>(IMessageOptions options, IClusterMessage message)
        where TResponse : IClusterMessage;

    IViewableDelegate<T> GetEvent<T>() where T : IClusterMessage;

    void ListenWithResponse<TRequest, TResponse>(IReadOnlyLifetime lifetime, Func<TRequest, Task<TResponse>> listener)
        where TRequest : IClusterMessage
        where TResponse : IClusterMessage;
}

public static class MessagingClientExtensions
{
    public static void ListenWithResponse<TRequest, TResponse>(
        this IMessagingClient client,
        IReadOnlyLifetime lifetime,
        Func<TRequest, TResponse> listener)
        where TRequest : IClusterMessage
        where TResponse : IClusterMessage
    {
        client.ListenWithResponse<TRequest, TResponse>(lifetime, Listener);

        Task<TResponse> Listener(TRequest request)
        {
            return Task.FromResult(listener(request));
        }
    }

    public static Task SendAll<T>(this IMessagingClient client, T message) where T : IClusterMessage
    {
        return client.Send(new AllMessageOptions(), message);
    }
}