using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Common.Reactive;
using Infrastructure.State;
using Microsoft.Extensions.Logging;

namespace Infrastructure;

public interface IStateCollection<TKey, TValue> : IReadOnlyDictionary<TKey, TValue>
{
    IViewableDelegate Updated { get; }

    // The record must be persisted already: other silos re-read it from storage.
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

// An update is a change notification: the receiver re-reads the record from storage (StateCollection.Refresh),
// so Value and UpdatedAt are not used for ordering. Kept in the message for compatibility with queued side effects.
[GenerateSerializer]
public class StateCollectionUpdate<TKey, TValue>
{
    [Id(0)] public required TKey Key { get; init; }
    [Id(1)] public required TValue Value { get; init; }
    [Id(2)] public DateTime UpdatedAt { get; init; }
}

// In-memory copy of all records of one state type, kept in sync through a durable queue.
//
// Ordering: the database is the source of truth. A notification only names the key; the collection re-reads the
// record, and the re-reads are serialized (_refreshLock), so every applied value is at least as new as the previous
// one, whatever order or how many times the notifications arrive. No timestamps from different machines are compared.
//
// Load vs subscribe: the subscription is made before the snapshot load, notifications that arrive during the load
// are buffered (_pending) and their keys are re-read after the snapshot is applied - none is lost.
//
// Reads (IReadOnlyDictionary) are lock free over a ConcurrentDictionary and may run on any thread.
public class StateCollection<TKey, TValue> :
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
    private readonly ConcurrentDictionary<TKey, TValue> _entries = new();
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private readonly object _pendingGate = new();

    // Keys notified before the snapshot is applied; null once the collection is loaded.
    private HashSet<TKey>? _pending = new();
    private int _isSetupStarted;

    public IViewableDelegate Updated => _updated;

    public bool IsLoaded
    {
        get
        {
            lock (_pendingGate)
                return _pending == null;
        }
    }

    public int Count => _entries.Count;
    public IEnumerable<TKey> Keys => _entries.Keys;
    public IEnumerable<TValue> Values => _entries.Values;
    public TValue this[TKey key] => _entries[key];

    public bool ContainsKey(TKey key) => _entries.ContainsKey(key);
    public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value) => _entries.TryGetValue(key, out value);
    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() => _entries.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public async Task OnLocalSetupCompleted(IReadOnlyLifetime lifetime)
    {
        if (Interlocked.Exchange(ref _isSetupStarted, 1) == 1)
            throw new InvalidOperationException($"StateCollection<{typeof(TKey).Name}, {typeof(TValue).Name}> is already set up");

        // Subscribe first: a record committed after the subscription is either in the snapshot or notified.
        await _utils.ListenUpdates(lifetime, OnNotified);

        // No try/catch: a failed or partial load must fail the service start, not leave an incomplete collection.
        var snapshot = await _utils.Load(lifetime);

        await _refreshLock.WaitAsync();

        try
        {
            foreach (var (key, value) in snapshot)
                _entries[key] = value;

            TKey[] buffered;

            lock (_pendingGate)
            {
                buffered = _pending!.ToArray();
                _pending = null;
            }

            if (buffered.Length != 0)
                await RefreshLocked(buffered);

            _logger.LogInformation("[StateCollection] Loaded {Count} entries for {Type}", Count, typeof(TValue).Name);
            _updated.Invoke();
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    public Task OnUpdated(TKey key, TValue value)
    {
        _entries[key] = value;
        return _utils.PushUpdate(key, value);
    }

    public Task OnUpdatedTransactional(TKey key, TValue value)
    {
        return _utils.PushTransactionalUpdate(key, value);
    }

    private Task OnNotified(TKey key)
    {
        lock (_pendingGate)
        {
            if (_pending != null)
            {
                _pending.Add(key);
                return Task.CompletedTask;
            }
        }

        return Refresh(key);
    }

    // Exceptions go back to the durable queue delivery: the side effect is retried, the update is not lost.
    private async Task Refresh(TKey key)
    {
        await _refreshLock.WaitAsync();

        try
        {
            await RefreshLocked([key]);

            // Under the lock: listeners are never invoked concurrently.
            _updated.Invoke();
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private async Task RefreshLocked(IReadOnlyList<TKey> keys)
    {
        var values = await _utils.Read(keys);

        foreach (var key in keys)
        {
            if (values.TryGetValue(key, out var value) == true)
                _entries[key] = value;
            else
                _entries.TryRemove(key, out _);
        }
    }
}
