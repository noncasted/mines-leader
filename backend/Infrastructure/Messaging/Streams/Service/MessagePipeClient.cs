using Common;
using Infrastructure.Orleans;
using Microsoft.Extensions.Logging;
using ServiceLoop;

namespace Infrastructure.Messaging;

public class MessagePipeClient : IMessagePipeClient, IOrleansLoopStage
{
    public MessagePipeClient(
        IOrleans orleans,
        ILogger<MessagePipeClient> logger)
    {
        _orleans = orleans;
        _logger = logger;
    }

    private readonly Dictionary<Type, object> _oneWayDelegates = new();
    private readonly Dictionary<string, MessagePipeObserver> _observers = new();

    private readonly IOrleans _orleans;
    private readonly ILogger<MessagePipeClient> _logger;

    private readonly List<Func<Task>> _resubscribeActions = new();

    public Task OnOrleansStage(IReadOnlyLifetime lifetime)
    {
        ResubscribeLoop(lifetime).NoAwait();
        return Task.CompletedTask;
    }

    public Task Send(IMessagePipeId id, object message)
    {
        var pipe = GetPipe(id);
        return pipe.Send(message);
    }

    public Task<TResponse> Send<TResponse>(IMessagePipeId id, object message)
    {
        var pipe = GetPipe(id);
        return pipe.Send<TResponse>(message);
    }

    public IViewableDelegate<T> GetOrCreateConsumer<T>(IMessagePipeId id)
    {
        var type = typeof(T);

        if (_oneWayDelegates.TryGetValue(type, out var existing) == true)
            return (ViewableDelegate<T>)existing;

        var source = new ViewableDelegate<T>();
        var observer = GetOrCreateObserver(id);

        observer.BindOneWayHandler(message =>
            {
                if (message is not T castedMessage)
                    throw new InvalidCastException($"Expected {typeof(T)}, but got {message.GetType()}");

                source.Invoke(castedMessage);
            }
        );

        _oneWayDelegates[type] = source;
        return source;
    }

    public void AddHandler<TRequest, TResponse>(
        IReadOnlyLifetime lifetime,
        IMessagePipeId id,
        Func<TRequest, Task<TResponse>> listener)
    {
        var observer = GetOrCreateObserver(id);

        observer.BindResponseHandler(async message =>
            {
                if (message is not TRequest castedMessage)
                    throw new InvalidCastException($"Expected {typeof(TRequest)}, but got {message.GetType()}");

                var response = await listener(castedMessage);

                if (response is not TResponse typedResponse)
                    throw new InvalidCastException($"Expected {typeof(TResponse)}, but got {response.GetType()}");

                return typedResponse;
            }
        );
    }

    private MessagePipeObserver GetOrCreateObserver(IMessagePipeId id)
    {
        var rawId = id.ToRaw();

        if (_observers.TryGetValue(rawId, out var existing) == true)
            return existing;

        var observer = new MessagePipeObserver(_logger);
        var observerReference = _orleans.Client.CreateObjectReference<IMessagePipeObserver>(observer);

        GC.KeepAlive(observer);
        GC.KeepAlive(observerReference);

        Subscribe().NoAwait();

        _resubscribeActions.Add(Subscribe);
        _observers[rawId] = observer;

        return observer;

        Task Subscribe()
        {
            try
            {
                return GetPipe(id).BindObserver(observerReference);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "[Messaging] [Queue] Failed to rebind observer to queue {QueueId}",
                    id.ToRaw()
                );
                return Task.CompletedTask;
            }
        }
    }

    private IMessagePipe GetPipe(IMessagePipeId id)
    {
        var rawId = id.ToRaw();
        return _orleans.GetGrain<IMessagePipe>(rawId);
    }
    
    private async Task ResubscribeLoop(IReadOnlyLifetime lifetime)
    {
        while (lifetime.IsTerminated == false)
        {
            await Task.WhenAll(_resubscribeActions.Select(t => t()));
            await Task.Delay(TimeSpan.FromSeconds(10), lifetime.Token);
        }
    }
}