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

    public async Task<IReadOnlyDictionary<TKey, TValue>> Load(IReadOnlyLifetime lifetime)
    {
        var stateInfo = _statesRegistry.Get<TValue>();
        var grainStateType = stateInfo.Type;

        if (!typeof(TValue).IsAssignableFrom(grainStateType))
        {
            _logger.LogError("[StateCollectionUtils] Type mismatch: {GrainType} is not assignable to {Expected}",
                grainStateType, typeof(TValue));
            return new Dictionary<TKey, TValue>();
        }

        var reader = _storage.ReadAll<TKey, TValue>(lifetime);

        var dictionary = new Dictionary<TKey, TValue>();

        try
        {
            await foreach (var (key, value) in reader)
                dictionary.Add(key, value);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[StateCollectionUtils] Failed to load {Type}, loaded {Count} entries before failure",
                typeof(TValue).Name, dictionary.Count);
        }

        return dictionary;
    }

    public Task PushUpdate(TKey key, TValue value)
    {
        return _messaging.PushDirectQueue(_queueId, new StateCollectionUpdate<TKey, TValue>
        {
            Key = key,
            Value = value,
            UpdatedAt = DateTime.UtcNow
        });
    }

    public Task PushTransactionalUpdate(TKey key, TValue value)
    {
        _messaging.PushTransactionalQueue(_queueId, new StateCollectionUpdate<TKey, TValue>
        {
            Key = key,
            Value = value,
            UpdatedAt = DateTime.UtcNow
        });

        return Task.CompletedTask;
    }

    public Task ListenUpdates(IReadOnlyLifetime lifetime, Action<TKey, TValue, DateTime> onUpdate)
    {
        return _messaging.ListenDurableQueue<StateCollectionUpdate<TKey, TValue>>(lifetime,
            _queueId,
            update => onUpdate(update.Key, update.Value, update.UpdatedAt));
    }
}