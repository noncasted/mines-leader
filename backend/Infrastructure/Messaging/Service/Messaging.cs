using Common;
using Infrastructure.Orleans;

namespace Infrastructure.Messaging;

public class Messaging : IMessaging
{
    public Messaging(IOrleans orleans)
    {
        _orleans = orleans;
    }

    private readonly IOrleans _orleans;

    private readonly Dictionary<Type, Func<IClusterMessage, Task<IClusterMessage>>> _asyncActions = new();
    private readonly Dictionary<Type, Action<IClusterMessage>> _actions = new();
    private readonly Dictionary<Type, object> _entries = new();

    public Task PushTransactionalQueue(IMessageQueueId id, IClusterMessage message)
    {
        var rawId = id.ToString();
        var grain = _orleans.GetGrain<IMessageQueue>(rawId);
        return grain.PushTransactional(message);
    }

    public Task PushDirectQueue(IMessageQueueId id, IClusterMessage message)
    {
        var rawId = id.ToString();
        var grain = _orleans.GetGrain<IMessageQueue>(rawId);
        return grain.PushDirect(message);
    }

    public void ListenQueue<T>(IReadOnlyLifetime lifetime, Action<T> listener) where T : IClusterMessage
    {
    }

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