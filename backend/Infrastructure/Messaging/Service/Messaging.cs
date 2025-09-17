using Common;
using Infrastructure.Orleans;

namespace Infrastructure.Messaging;

public class Messaging : IMessaging
{
    public Messaging(IOrleans orleans, IMessageQueueClient queue)
    {
        _orleans = orleans;
        Queue = queue;
    }

    private readonly IOrleans _orleans;

    public IMessageQueueClient Queue { get; }

    public async Task SendStream(IMessageOptions options, IClusterMessage message)
    {
    }

    public Task<TResponse> SendStream<TResponse>(IMessageOptions options, IClusterMessage message)
        where TResponse : IClusterMessage
    {
        return null;
    }

    public IViewableDelegate<T> GetStreamDelegate<T>() where T : IClusterMessage
    {
        return null;
    }

    public void ListenStream<TRequest, TResponse>(IReadOnlyLifetime lifetime, Func<TRequest, Task<TResponse>> listener)
        where TRequest : IClusterMessage
        where TResponse : IClusterMessage
    {
    }
}