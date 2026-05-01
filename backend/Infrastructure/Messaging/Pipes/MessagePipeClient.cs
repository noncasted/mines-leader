using System.Collections.Concurrent;
using Common.Extensions;
using Common.Reactive;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Infrastructure;

public interface IRuntimePipeClient
{
    Task Start(IReadOnlyLifetime lifetime);

    Task<TResponse> Send<TResponse>(IRuntimePipeId id, object message);

    Task<bool> Exists(IRuntimePipeId id);

    Task AddHandler<TRequest, TResponse>(
        IReadOnlyLifetime lifetime,
        IRuntimePipeId id,
        Func<TRequest, Task<TResponse>> listener);
}

public class RuntimePipeClient : IRuntimePipeClient
{
    public RuntimePipeClient(
        IOrleans orleans,
        IServiceProvider services,
        ILogger<RuntimePipeClient> logger)
    {
        _orleans = orleans;
        _config = new Lazy<IRuntimePipeConfig>(() => services.GetRequiredService<IRuntimePipeConfig>());
        _logger = logger;
    }

    private readonly IOrleans _orleans;
    private readonly Lazy<IRuntimePipeConfig> _config;
    private readonly ILogger<RuntimePipeClient> _logger;

    private readonly ConcurrentDictionary<Guid, Listener> _listeners = new();

    public Task Start(IReadOnlyLifetime lifetime)
    {
        ResubscribeLoop(lifetime).NoAwait();
        return Task.CompletedTask;
    }

    public async Task<TResponse> Send<TResponse>(IRuntimePipeId id, object message)
    {
        var pipe = GetPipe(id);
        var options = _config.Value.Value;

        Exception? lastException = null;

        for (var attempt = 0; attempt <= options.SendRetryCount; attempt++)
        {
            try
            {
                return await pipe.Send<TResponse>(message);
            }
            catch (Exception e) when (IsTransient(e))
            {
                lastException = e;

                if (attempt < options.SendRetryCount)
                {
                    BackendMetrics.PipeRetry.Add(1);
                    var delay = options.SendRetryBaseDelayMs * (1 << attempt);

                    _logger.LogWarning(e,
                        "[Messaging] [Pipe] Send to {PipeId} failed (attempt {Attempt}/{Max}), retrying in {Delay}ms",
                        id.ToRaw(), attempt + 1, options.SendRetryCount + 1, delay);

                    await Task.Delay(delay);
                }
            }
        }

        throw lastException!;
    }

    private static bool IsTransient(Exception e)
    {
        if (ContainsHandlerFailure(e))
            return false;

        return e is not (InvalidCastException or ArgumentException or NotSupportedException);
    }

    private static bool ContainsHandlerFailure(Exception e)
    {
        var current = e;

        while (current != null)
        {
            if (current.Message.Contains(RuntimePipeObserver.HandlerFailurePrefix, StringComparison.Ordinal))
                return true;

            current = current.InnerException;
        }

        return false;
    }

    public async Task<bool> Exists(IRuntimePipeId id)
    {
        try
        {
            return await GetPipe(id).HasObserver();
        }
        catch (Exception e)
        {
            _logger.LogWarning(e, "[Messaging] [Pipe] Exists check for {PipeId} failed", id.ToRaw());
            return false;
        }
    }

    public async Task AddHandler<TRequest, TResponse>(
        IReadOnlyLifetime lifetime,
        IRuntimePipeId id,
        Func<TRequest, Task<TResponse>> listener)
    {
        if (lifetime.IsTerminated)
            return;

        var observer = new RuntimePipeObserver(_logger);
        observer.BindResponseHandler(async message => {
            if (message is not TRequest castedMessage)
                throw new InvalidCastException($"Expected {typeof(TRequest)}, but got {message.GetType()}");

            var response = await listener(castedMessage);

            if (response is null)
                throw new InvalidCastException($"Expected {typeof(TResponse)}, but got null");

            return response;
        });

        await RegisterObserver(lifetime, id, observer);
    }

    private async Task RegisterObserver(IReadOnlyLifetime lifetime, IRuntimePipeId id, RuntimePipeObserver observer)
    {
        var observerId = Guid.NewGuid();
        var observerReference = _orleans.Client.CreateObjectReference<IRuntimePipeObserver>(observer);

        foreach (var (checkId, checkListener) in _listeners.ToArray())
        {
            if (checkListener.Id.ToRaw() != id.ToRaw())
                continue;

            _logger.LogWarning("[Messaging] [RuntimePipe] Removing duplicate observer {ObserverId} for pipe {PipeId}",
                checkListener.ObserverId, id.ToRaw());
            RemoveListener(checkId, checkListener);
        }

        var transportListener = new Listener(
            id,
            observerId,
            observer,
            observerReference,
            GetPipe(id),
            _orleans,
            _logger);

        _listeners[observerId] = transportListener;
        lifetime.Listen(() => RemoveListener(observerId, transportListener));

        try
        {
            await transportListener.Resubscribe();
            transportListener.RecordSuccess();
        }
        catch (Exception e)
        {
            transportListener.RecordFailure(e);
            RemoveListener(observerId, transportListener);
            throw;
        }
    }

    private void RemoveListener(Guid listenerId, Listener listener)
    {
        var listeners = (ICollection<KeyValuePair<Guid, Listener>>)_listeners;

        if (listeners.Remove(new KeyValuePair<Guid, Listener>(listenerId, listener)))
            listener.Cleanup();
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
            if (_listeners.IsEmpty)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(10), lifetime.Token);
                }
                catch (OperationCanceledException) when (lifetime.IsTerminated)
                {
                }

                continue;
            }

            var snapshot = _listeners.Values.ToArray();

            foreach (var listener in snapshot)
            {
                try
                {
                    await listener.Resubscribe();
                    listener.RecordSuccess();
                }
                catch (Exception e)
                {
                    listener.RecordFailure(e);
                }
            }

            var delay = snapshot.Length == 0
                ? TimeSpan.FromSeconds(10)
                : snapshot.Min(l => l.Interval.GetNextDelay());

            try
            {
                await Task.Delay(delay, lifetime.Token);
            }
            catch (OperationCanceledException) when (lifetime.IsTerminated)
            {
            }
        }
    }

    public class Listener
    {
        public Listener(
            IRuntimePipeId id,
            Guid observerId,
            RuntimePipeObserver observerSource,
            IRuntimePipeObserver observer,
            IRuntimePipe pipe,
            IOrleans orleans,
            ILogger logger)
        {
            Id = id;
            ObserverId = observerId;
            ObserverSource = observerSource;
            Observer = observer;
            Pipe = pipe;
            Orleans = orleans;
            Logger = logger;
        }

        private int _cleanupStarted;
        private int _consecutiveFailures;

        public IRuntimePipeId Id { get; }
        public Guid ObserverId { get; }
        public RuntimePipeObserver ObserverSource { get; }
        public IRuntimePipeObserver Observer { get; }
        public IRuntimePipe Pipe { get; }
        public IOrleans Orleans { get; }
        public ILogger Logger { get; }

        public AdaptiveInterval Interval { get; } = new(minInterval: TimeSpan.FromSeconds(10),
            maxInterval: TimeSpan.FromSeconds(60),
            failureBaseInterval: TimeSpan.FromSeconds(1));

        public async Task Resubscribe()
        {
            await Pipe.BindObserver(ObserverId, Observer);
        }

        public void RecordSuccess()
        {
            _consecutiveFailures = 0;
            Interval.RecordSuccess();
        }

        public void RecordFailure(Exception e)
        {
            _consecutiveFailures++;
            Interval.RecordFailure();

            if (_consecutiveFailures == 1 || _consecutiveFailures % 10 == 0)
            {
                Logger.LogError(e,
                    "[Messaging] [RuntimePipe] Failed to rebind observer {ObserverId} (attempt {Count}) to pipe {PipeId}",
                    ObserverId, _consecutiveFailures, Id.ToRaw());
            }
        }

        public void Cleanup()
        {
            if (Interlocked.Exchange(ref _cleanupStarted, 1) == 1)
                return;

            CleanupTransport().NoAwait();
        }

        private async Task CleanupTransport()
        {
            ObserverSource.ClearResponseHandler();

            try
            {
                await Pipe.UnbindObserver(ObserverId);
            }
            catch (Exception e)
            {
                Logger.LogWarning(e,
                    "[Messaging] [RuntimePipe] Failed to unbind observer {ObserverId} from pipe {PipeId}",
                    ObserverId, Id.ToRaw());
            }

            try
            {
                Orleans.Client.DeleteObjectReference<IRuntimePipeObserver>(Observer);
            }
            catch (Exception e)
            {
                Logger.LogWarning(e,
                    "[Messaging] [RuntimePipe] Failed to delete object reference for observer {ObserverId} on pipe {PipeId}",
                    ObserverId, Id.ToRaw());
            }
        }
    }
}
