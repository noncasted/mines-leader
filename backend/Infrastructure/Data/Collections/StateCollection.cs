using Common.Reactive;
using Infrastructure.State;

namespace Infrastructure;

public interface IStateCollection<TKey, TValue> : IReadOnlyDictionary<TKey, TValue>
{
    IViewableDelegate Updated { get; }

    Task OnUpdated(TKey key, TValue value);
    Task OnUpdatedTransactional(TKey key, TValue value);
}

public class StateCollectionMessageQueueId<TKey, TValue> : IMessageQueueId
{
    public string ToRaw()
    {
        return $"state-collection-{typeof(TKey).FullName}-{typeof(TValue).FullName}";
    }
}

[GenerateSerializer]
public class StateCollectionUpdate<TKey, TValue>
{
    [Id(0)] public required TKey Key { get; init; }
    [Id(1)] public required TValue Value { get; init; }
}

public class StateCollectionUtils<TKey, TValue>
    where TKey : notnull
    where TValue : class, IStateValue, new()
{
    public StateCollectionUtils(
        IGrainStatesRegistry statesRegistry,
        IStateStorage storage,
        IMessaging messaging)
    {
        _statesRegistry = statesRegistry;
        _storage = storage;
        _messaging = messaging;
    }

    private readonly IGrainStatesRegistry _statesRegistry;
    private readonly IStateStorage _storage;
    private readonly IMessaging _messaging;

    private readonly StateCollectionMessageQueueId<TKey, TValue> _queueId = new();

    public async Task<IReadOnlyDictionary<TKey, TValue>> Load(IReadOnlyLifetime lifetime)
    {
        var stateInfo = _statesRegistry.Get<TValue>();
        var grainStateType = stateInfo.Type;

        if (!typeof(TValue).IsAssignableFrom(grainStateType))
            throw new Exception($"State type {grainStateType} is not assignable to {typeof(TValue)}");

        var reader = _storage.ReadAll<TKey, TValue>(lifetime);

        var dictionary = new Dictionary<TKey, TValue>();

        await foreach (var (key, value) in reader)
            dictionary.Add(key, value);

        return dictionary;
    }

    public Task PushUpdate(TKey key, TValue value)
    {
        return _messaging.PushDirectQueue(_queueId, new StateCollectionUpdate<TKey, TValue>
            {
                Key = key,
                Value = value
            }
        );
    }

    public Task PushTransactionalUpdate(TKey key, TValue value)
    {
        _messaging.PushTransactionalQueue(_queueId, new StateCollectionUpdate<TKey, TValue>
            {
                Key = key,
                Value = value
            }
        );

        return Task.CompletedTask;
    }

    public Task ListenUpdates(IReadOnlyLifetime lifetime, Action<TKey, TValue> onUpdate)
    {
        return _messaging.ListenQueue<StateCollectionUpdate<TKey, TValue>>(
            lifetime,
            _queueId,
            update => onUpdate(update.Key, update.Value)
        );
    }
}

public class StateCollection<TKey, TValue> :
    Dictionary<TKey, TValue>,
    IStateCollection<TKey, TValue>,
    ILocalSetupCompleted
    where TKey : notnull
    where TValue : class, IStateValue, new()
{
    public StateCollection(StateCollectionUtils<TKey, TValue> utils)
    {
        _utils = utils;
    }

    private readonly StateCollectionUtils<TKey, TValue> _utils;
    private readonly ViewableDelegate _updated = new();

    public IViewableDelegate Updated => _updated;

    public async Task OnLocalSetupCompleted(IReadOnlyLifetime lifetime)
    {
        await _utils.ListenUpdates(lifetime, (key, value) =>
            {
                this[key] = value;
                _updated.Invoke();
            }
        );

        var existing = await _utils.Load(lifetime);

        foreach (var (key, value) in existing)
            this[key] = value;
    }

    public Task OnUpdated(TKey key, TValue value)
    {
        this[key] = value;
        return _utils.PushUpdate(key, value);
    }

    public Task OnUpdatedTransactional(TKey key, TValue value)
    {
        return _utils.PushTransactionalUpdate(key, value);
    }
}