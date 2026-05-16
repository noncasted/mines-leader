using Common.Reactive;
using Infrastructure.State;
using Microsoft.Extensions.Logging;

namespace Infrastructure;

public interface IStateCollection<TKey, TValue> : IReadOnlyDictionary<TKey, TValue>
{
    IViewableDelegate Updated { get; }

    Task OnUpdated(TKey key, TValue value);
    Task OnUpdatedTransactional(TKey key, TValue value);
}

public class StateCollectionDurableQueueId<TKey, TValue> : IDurableQueueId
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
    [Id(2)] public DateTime UpdatedAt { get; init; }
}

public class StateCollection<TKey, TValue> :
    Dictionary<TKey, TValue>,
    IStateCollection<TKey, TValue>,
    ILocalSetupCompleted
    where TKey : notnull
    where TValue : class, IStateValue, new()
{
    public StateCollection(StateCollectionUtils<TKey, TValue> utils, ILogger<StateCollection<TKey, TValue>> logger)
    {
        _utils = utils;
        _logger = logger;
    }

    private readonly StateCollectionUtils<TKey, TValue> _utils;
    private readonly ILogger<StateCollection<TKey, TValue>> _logger;
    private readonly ViewableDelegate _updated = new();
    private readonly Dictionary<TKey, DateTime> _lastUpdated = new();

    public IViewableDelegate Updated => _updated;

    public async Task OnLocalSetupCompleted(IReadOnlyLifetime lifetime)
    {
        try
        {
            var existing = await _utils.Load(lifetime);

            foreach (var (key, value) in existing)
                this[key] = value;

            await _utils.ListenUpdates(lifetime, (key, value, updatedAt) => {
                if (_lastUpdated.TryGetValue(key, out var last) && updatedAt <= last)
                    return;

                this[key] = value;
                _lastUpdated[key] = updatedAt;
                _updated.Invoke();
            });

            _logger.LogInformation("[StateCollection] Loaded {Count} entries for {Type}", Count, typeof(TValue).Name);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[StateCollection] Failed to initialize {Type}", typeof(TValue).Name);
        }
    }

    public Task OnUpdated(TKey key, TValue value)
    {
        this[key] = value;
        _lastUpdated[key] = DateTime.UtcNow;
        return _utils.PushUpdate(key, value);
    }

    public Task OnUpdatedTransactional(TKey key, TValue value)
    {
        return _utils.PushTransactionalUpdate(key, value);
    }
}