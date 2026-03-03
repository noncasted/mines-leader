using System.Collections.Concurrent;
using Common.Extensions;
using Common.Reactive;
using Microsoft.Extensions.Logging;

namespace Infrastructure;

public interface IMessagePipeClient
{
    Task Start(IReadOnlyLifetime lifetime);

    Task Send(IMessagePipeId id, object message);
    Task<TResponse> Send<TResponse>(IMessagePipeId id, object message);
    Task<IViewableDelegate<T>> CreateListener<T>(IReadOnlyLifetime lifetime, IMessagePipeId id);

    Task AddHandler<TRequest, TResponse>(
        IReadOnlyLifetime lifetime,
        IMessagePipeId id,
        Func<TRequest, Task<TResponse>> listener);
}

public class MessagePipeClient : IMessagePipeClient
{
    public MessagePipeClient(
        IOrleans orleans,
        ILogger<MessagePipeClient> logger)
    {
        _orleans = orleans;
        _logger = logger;
    }

    private readonly IOrleans _orleans;
    private readonly ILogger<MessagePipeClient> _logger;

    private readonly ConcurrentDictionary<Guid, Listener> _listeners = new();

    public Task Start(IReadOnlyLifetime lifetime)
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

    public async Task<IViewableDelegate<T>> CreateListener<T>(IReadOnlyLifetime lifetime, IMessagePipeId id)
    {
        var source = new ViewableDelegate<T>();
        var observer = await CreateObserver(lifetime, id);

        observer.BindOneWayHandler(message =>
            {
                if (message is not T castedMessage)
                    throw new InvalidCastException($"Expected {typeof(T)}, but got {message.GetType()}");

                source.Invoke(castedMessage);
            }
        );

        return source;
    }

    public async Task AddHandler<TRequest, TResponse>(
        IReadOnlyLifetime lifetime,
        IMessagePipeId id,
        Func<TRequest, Task<TResponse>> listener)
    {
        var observer = await CreateObserver(lifetime, id);

        observer.BindResponseHandler(async message =>
            {
                if (message is not TRequest castedMessage)
                    throw new InvalidCastException($"Expected {typeof(TRequest)}, but got {message.GetType()}");

                var response = await listener(castedMessage);

                if (response is null)
                    throw new InvalidCastException($"Expected {typeof(TResponse)}, but got {response!.GetType()}");

                return response;
            }
        );
    }

    private async Task<MessagePipeObserver> CreateObserver(IReadOnlyLifetime lifetime, IMessagePipeId id)
    {
        var observer = new MessagePipeObserver(_logger);
        var observerReference = _orleans.Client.CreateObjectReference<IMessagePipeObserver>(observer);
        
        lifetime.Listen(() => _orleans.Client.DeleteObjectReference<IMessagePipeObserver>(observerReference));

        var toRemove = new List<Guid>();

        foreach (var (checkId, checkListener) in _listeners)
        {
            if (checkListener.Id.ToRaw() != id.ToRaw())
                continue;

            toRemove.Add(checkId);
        }

        foreach (var removeId in toRemove)
        {
            _logger.LogWarning("[Messaging] [Pipe] Removing duplicate observer for pipe {PipeId}", id.ToRaw());
            _listeners.Remove(removeId, out _);
        }

        var listenerId = Guid.NewGuid();

        var listener = new Listener
        {
            Id = id,
            ObserverSource = observer,
            Observer = observerReference,
            Pipe = GetPipe(id),
            Logger = _logger
        };

        _listeners.AddOrUpdate(listenerId, _ => listener, (_, __) => listener);
        lifetime.Listen(() => _listeners.Remove(listenerId, out _));

        await listener.Resubscribe();

        return observer;
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
            if (_listeners.Count == 0)
            {
                await Task.Delay(TimeSpan.FromSeconds(10), lifetime.Token);
                continue;
            }

            await Task.WhenAll(_listeners.Select(t => t.Value.Resubscribe()));
            await Task.Delay(TimeSpan.FromSeconds(120), lifetime.Token);
        }
    }

    public class Listener
    {
        public required IMessagePipeId Id { get; init; }
        public required MessagePipeObserver ObserverSource { get; init; }
        public required IMessagePipeObserver Observer { get; init; }
        public required IMessagePipe Pipe { get; init; }
        public required ILogger Logger { get; init; }

        public Task Resubscribe()
        {
            try
            {
                return Pipe.BindObserver(Observer);
            }
            catch (Exception e)
            {
                Logger.LogError(e, "[Messaging] [Pipe] Failed to rebind observer to pipe {QueueId}",
                    Id.ToRaw()
                );

                return Task.CompletedTask;
            }
        }
    }
}