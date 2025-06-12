using Common;
using Infrastructure.Discovery;

namespace Infrastructure.Messaging;

public static class MessagingExtensions
{
    public static IMessagingHub GetMessagingHub(this IGrainFactory grainFactory)
    {
        return grainFactory.GetGrain<IMessagingHub>(Guid.Empty);
    }

    public static void Listen<T>(
        this IMessagingClient messaging,
        IReadOnlyLifetime lifetime,
        Action<T> listener) where T : IClusterMessage
    {
        var source = messaging.GetEvent<T>();
        source.Advise(lifetime, listener);
    }
    
    public static void Listen<T>(
        this IMessagingClient messaging,
        IReadOnlyLifetime lifetime,
        Func<T, Task> listener) where T : IClusterMessage
    {
        var source = messaging.GetEvent<T>();
        source.Advise(lifetime, value => Task.FromResult(listener.Invoke(value)));
    }

    public static Task Send<T>(this IMessagingClient messaging, Guid targetId, T message) where T : IClusterMessage
    {
        var options = new MessageOptions
        {
            TargetId = targetId
        };

        return messaging.Send(options, message);
    }

    public static Task Send<T>(this IMessagingClient messaging, ServiceTag tag, T message) where T : IClusterMessage
    {
        var options = new TagMessageOptions
        {
            Tag = tag
        };

        return messaging.Send(options, message);
    }

    public static Task<TResponse> Send<TResponse>(
        this IMessagingClient messaging,
        Guid targetId,
        IClusterMessage request)
        where TResponse : IClusterMessage
    {
        var options = new MessageOptions
        {
            TargetId = targetId
        };

        return messaging.Send<TResponse>(options, request);
    }
}