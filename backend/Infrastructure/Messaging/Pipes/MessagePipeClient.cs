using System.Collections.Concurrent;
using Common.Extensions;
using Common.Reactive;
using Microsoft.Extensions.Logging;

namespace Infrastructure;

public interface IRuntimePipeClient
{
    Task Start(IReadOnlyLifetime lifetime);

    Task<TResponse> Send<TResponse>(IRuntimePipeId id, object message);

    Task AddHandler<TRequest, TResponse>(
        IReadOnlyLifetime lifetime,
        IRuntimePipeId id,
        Func<TRequest, Task<TResponse>> listener);
}

public class RuntimePipeClient : IRuntimePipeClient
{
    public RuntimePipeClient(
        IOrleans orleans,
        ILogger<RuntimePipeClient> logger)
    {
        _orleans = orleans;
        _logger = logger;
    }

    private readonly IOrleans _orleans;
    private readonly ILogger<RuntimePipeClient> _logger;

    private readonly ConcurrentDictionary<Guid, Listener> _listeners = new();

    public Task Start(IReadOnlyLifetime lifetime)
    {
        ResubscribeLoop(lifetime).NoAwait();
        return Task.CompletedTask;
    }

    public Task<TResponse> Send<TResponse>(IRuntimePipeId id, object message)
    {
        var pipe = GetPipe(id);
        return pipe.Send<TResponse>(message);
    }

    public async Task AddHandler<TRequest, TResponse>(
        IReadOnlyLifetime lifetime,
        IRuntimePipeId id,
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

    private async Task<RuntimePipeObserver> CreateObserver(IReadOnlyLifetime lifetime, IRuntimePipeId id)
    {
        var observer = new RuntimePipeObserver(_logger);
        var observerReference = _orleans.Client.CreateObjectReference<IRuntimePipeObserver>(observer);

        lifetime.Listen(() => _orleans.Client.DeleteObjectReference<IRuntimePipeObserver>(observerReference));

        var toRemove = new List<Guid>();

        foreach (var (checkId, checkListener) in _listeners)
        {
            if (checkListener.Id.ToRaw() != id.ToRaw())
                continue;

            toRemove.Add(checkId);
        }

        foreach (var removeId in toRemove)
        {
            _logger.LogWarning("[Messaging] [RuntimePipe] Removing duplicate observer for pipe {PipeId}", id.ToRaw());
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

    private IRuntimePipe GetPipe(IRuntimePipeId id)
    {
        var rawId = id.ToRaw();
        return _orleans.GetGrain<IRuntimePipe>(rawId);
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
        public required IRuntimePipeId Id { get; init; }
        public required RuntimePipeObserver ObserverSource { get; init; }
        public required IRuntimePipeObserver Observer { get; init; }
        public required IRuntimePipe Pipe { get; init; }
        public required ILogger Logger { get; init; }

        private int _consecutiveFailures;

        public async Task Resubscribe()
        {
            try
            {
                await Pipe.BindObserver(Observer);
                _consecutiveFailures = 0;
            }
            catch (Exception e)
            {
                _consecutiveFailures++;

                if (_consecutiveFailures == 1 || _consecutiveFailures % 10 == 0)
                    Logger.LogError(e,
                        "[Messaging] [RuntimePipe] Failed to rebind observer (attempt {Count}) to pipe {PipeId}",
                        _consecutiveFailures, Id.ToRaw()
                    );
            }
        }
    }
}
