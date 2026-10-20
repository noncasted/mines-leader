using Common.Reactive;
using Infrastructure.State;
using Microsoft.Extensions.Logging;

namespace Infrastructure;

public class StateCollectionUtils<TKey, TValue>
    where TKey : notnull
    where TValue : class, IStateValue, new()
{
    public StateCollectionUtils(
        IGrainStatesRegistry statesRegistry,
        IStateStorage storage,
        IMessaging messaging,
        ILogger<StateCollectionUtils<TKey, TValue>> logger)
    {
        _statesRegistry = statesRegistry;
        _storage = storage;
        _messaging = messaging;
        _logger = logger;
    }

    private readonly IGrainStatesRegistry _statesRegistry;
    private readonly IStateStorage _storage;
    private readonly IMessaging _messaging;
    private readonly ILogger<StateCollectionUtils<TKey, TValue>> _logger;

    private readonly StateCollectionDurableQueueId<TKey, TValue> _queueId = new();

    // Throws on any failure: a partial snapshot is never returned.
    public virtual async Task<IReadOnlyDictionary<TKey, TValue>> Load(IReadOnlyLifetime lifetime)
    {
        GetStateInfo();

        var dictionary = new Dictionary<TKey, TValue>();

        await foreach (var (key, value) in _storage.ReadAll<TKey, TValue>(lifetime))
            dictionary.Add(key, value);

        _logger.LogDebug("[StateCollectionUtils] Loaded {Count} {Type} entries", dictionary.Count, typeof(TValue).Name);

        return dictionary;
    }

    // Current stored values; a key missing from the result has no record.
    public virtual Task<IReadOnlyDictionary<TKey, TValue>> Read(IReadOnlyList<TKey> keys)
    {
        var stateInfo = GetStateInfo();

        var identities = keys.Select(key => new StateIdentity
        {
            Key = key,
            Type = stateInfo.Name,
            TableName = stateInfo.TableName,
            Extension = null
        }).ToList();

        return _storage.ReadBatch<TKey, TValue>(identities);
    }

    public virtual Task PushUpdate(TKey key, TValue value)
    {
        return _messaging.PushDirectQueue(_queueId, new StateCollectionUpdate<TKey, TValue>
        {
            Key = key,
            Value = value,
            UpdatedAt = DateTime.UtcNow
        });
    }

    public virtual Task PushTransactionalUpdate(TKey key, TValue value)
    {
        _messaging.PushTransactionalQueue(_queueId, new StateCollectionUpdate<TKey, TValue>
        {
            Key = key,
            Value = value,
            UpdatedAt = DateTime.UtcNow
        });

        return Task.CompletedTask;
    }

    public virtual Task ListenUpdates(IReadOnlyLifetime lifetime, Func<TKey, Task> onUpdate)
    {
        return _messaging.ListenDurableQueue<StateCollectionUpdate<TKey, TValue>>(lifetime,
            _queueId,
            update => onUpdate(update.Key));
    }

    private GrainStateInfo GetStateInfo()
    {
        var stateInfo = _statesRegistry.Get<TValue>();

        if (typeof(TValue).IsAssignableFrom(stateInfo.Type) == false)
        {
            throw new InvalidOperationException(
                $"[StateCollectionUtils] Type mismatch: {stateInfo.Type} is not assignable to {typeof(TValue)}");
        }

        return stateInfo;
    }
}
