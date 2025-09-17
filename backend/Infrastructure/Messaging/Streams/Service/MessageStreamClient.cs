using Common;
using Infrastructure.Messaging;
using Infrastructure.Orleans;
using Microsoft.Extensions.Logging;
using Orleans.Configuration;
using ServiceLoop;

namespace Service;

public class MessageStreamClient : IMessageStreamClient, IOrleansLoopStage
{
    public MessageStreamClient(
        IOrleans orleans,
        ILogger<MessageStreamClient> logger)
    {
        _orleans = orleans;
        _logger = logger;
    }

    private readonly Dictionary<Type, Func<object, Task<object>>> _asyncActions = new();
    private readonly Dictionary<Type, Action<IClusterMessage>> _actions = new();
    private readonly Dictionary<Type, object> _entries = new();

    private readonly IOrleans _orleans;
    private readonly ILogger<MessageStreamClient> _logger;
    
    private readonly List<Func<Task>> _resubscribeActions = new();

    public async Task OnOrleansStage(IReadOnlyLifetime lifetime)
    {
        _hub = _orleans.GetMessagingHub();
        _observer = new MessagingListener();
        _observerReference = _orleans.CreateObjectReference<IMessagingListener>(_observer);

        GC.KeepAlive(_observer);
        GC.KeepAlive(_observerReference);

        _observer.MessageReceived.Advise(lifetime, OnReceived);
        _observer.ResponseMessageReceived.Advise(lifetime, OnReceivedWithResponse);

        var isSuccess = false;

        while (isSuccess == false && lifetime.IsTerminated == false)
        {
            try
            {
                var overview = new MessagingObserverOverview
                {
                    ServiceId = _environment.ServiceId,
                    Name = _environment.ServiceName,
                    Tag = _environment.Tag
                };

                await _hub.BindClient(overview, _observerReference);
                isSuccess = true;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "[Messaging] Failed to bind client");
                await Task.Delay(MessagingOptions.ResubscriptionTime, lifetime.Token);
            }
        }

        BindLoop(lifetime).NoAwait();
    }
    
    public Task Send(IMessageOptions options, object message)
    {
        throw new NotImplementedException();
    }

    public Task<TResponse> Send<TResponse>(IMessageOptions options, object message)
    {
        throw new NotImplementedException();
    }

    public IViewableDelegate<T> GetOrCreateConsumer<T>(IMessageStreamId id)
    {
        var type = typeof(T);

        if (_entries.ContainsKey(type) == false)
        {
            var source = new ViewableDelegate<T>();

            _actions[type] = payload =>
            {
                if (payload is not T castedPayload)
                    throw new InvalidCastException();

                source.Invoke(castedPayload);
            };
            
            var observer = new MessageStreamObserver(
                message =>
                {
                    if (message is not T castedMessage)
                        throw new InvalidCastException();

                    source.Invoke(castedMessage);
                },
                message =>
                {
                    if (message is not T castedMessage)
                        throw new InvalidCastException();

                    source.Invoke(castedMessage);
                    return Task.FromResult(message);
                }
            );

            var observerReference = _orleans.Client.CreateObjectReference<IMessageQueueObserver>(observer);

            GC.KeepAlive(observer);
            GC.KeepAlive(observerReference);

            Subscribe().NoAwait();
            _resubscribeActions.Add(Subscribe);

            _entries[type] = source;
            
            Task Subscribe()
            {
                try
                {
                    return GetStream(id).BindObserver(observerReference);
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "[Messaging] [Queue] Failed to rebind observer to queue {QueueId}",
                        id.ToString()
                    );
                    return Task.CompletedTask;
                }
            }
        }

        return (ViewableDelegate<T>)_entries[type];
    }

    public void AddHandler<TRequest, TResponse>(
        IReadOnlyLifetime lifetime,
        IMessageStreamId id,
        Func<TRequest, Task<TResponse>> listener)
    {
        throw new NotImplementedException();
    }

    public Task Send(IMessageOptions options, IClusterMessage message)
    {
        var hub = _orleans.GetMessagingHub();
        return hub.Send(options, message);
    }

    public Task<TResponse> Send<TResponse>(IMessageOptions options, IClusterMessage message)
        where TResponse : IClusterMessage
    {
        var hub = _orleans.GetMessagingHub();
        return hub.Send<TResponse>(options, message);
    }

    public IViewableDelegate<T> GetEvent<T>() where T : IClusterMessage
    {
     
    }

    public void ListenWithResponse<TRequest, TResponse>(
        IReadOnlyLifetime lifetime,
        Func<TRequest, Task<TResponse>> listener)
        where TRequest : IClusterMessage
        where TResponse : IClusterMessage
    {
        var type = typeof(TRequest);

        if (_asyncActions.ContainsKey(type) == true)
            throw new Exception($"Event for {type.Name} already registered");

        var callback = Listener;
        _asyncActions[type] = callback;

        lifetime.Listen(() =>
            {
                if (_asyncActions[type] != callback)
                    return;

                _asyncActions.Remove(type);
            }
        );

        async Task<IClusterMessage> Listener(IClusterMessage payload)
        {
            try
            {
                var result = await listener.Invoke((TRequest)payload);
                return result;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "[Messaging] Error in listener for {MessageType}", type.Name);
                throw;
            }
        }
    }

    private async Task BindLoop(IReadOnlyLifetime lifetime)
    {
        while (lifetime.IsTerminated == false)
        {
            try
            {
                var overview = new MessagingObserverOverview
                {
                    ServiceId = _environment.ServiceId,
                    Name = _environment.ServiceName,
                    Tag = _environment.Tag
                };

                await _hub!.BindClient(overview, _observerReference!);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "[Messaging] Failed to bind client");
            }

            await Task.Delay(MessagingOptions.ResubscriptionTime, lifetime.Token);
        }
    }

    private void OnReceived(IClusterMessage message)
    {
        var type = message.GetType();

        if (_actions.TryGetValue(type, out var action) == false)
            throw new InvalidOperationException($"Event type {type} not found in actions.");

        action.Invoke(message);
    }

    private void OnReceivedWithResponse(IClusterMessage message, Action<Task<IClusterMessage>> callback)
    {
        var type = message.GetType();

        if (_asyncActions.TryGetValue(type, out var action) == false)
            throw new InvalidOperationException($"Event type {type} not found in actions.");

        var invoke = action.Invoke(message);
        callback.Invoke(invoke);
    }
    
    private IMessageStream GetStream(IMessageStreamId id)
    {
        var rawId = id.ToString();
        return _orleans.GetGrain<IMessageStream>(rawId);
    }

}