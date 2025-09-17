using Infrastructure.Orleans;
using Microsoft.Extensions.Logging;
using Orleans.Concurrency;

namespace Infrastructure.Messaging;

[Reentrant]
public class MessagingHub : Grain, IMessagingHub
{
    private readonly MessagingObserversCollection _observersCollection;
    private readonly ILogger<MessagingHub> _logger;
    
    // ReSharper disable once NotAccessedField.Local
    private readonly GrainDeactivationLocker _deactivationLocker;

    public MessagingHub(
        IGrainContextAccessor contextAccessor,
        MessagingObserversCollection observersCollection,
        ILogger<MessagingHub> logger)
    {
        _observersCollection = observersCollection;
        _logger = logger;
        _deactivationLocker = new GrainDeactivationLocker(contextAccessor);
    }

    public Task BindClient(MessagingObserverOverview overview, IMessagingListener listener)
    {
        _observersCollection.Add(overview, listener);
        return Task.CompletedTask;
    }

    public Task Send(IMessageOptions options, IClusterMessage message)
    {
        return _observersCollection.Notify(options.IsTarget, observer => observer.Send(message));
    }

    public async Task<TResponse> Send<TResponse>(IMessageOptions options, IClusterMessage message)
        where TResponse : IClusterMessage
    {
        var results = new List<TResponse>();

        await _observersCollection.Notify(options.IsTarget, async observer =>
        {
            var result = await observer.Send<TResponse>(message);

            results.Add(result);
        });

        if (results.Count > 1)
        {
            _logger.LogError("[Messaging] Multiple responses received for message {MessageType}",
                message.GetType().Name);
        }

        return results.First();
    }
}