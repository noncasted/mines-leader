using Infrastructure.Execution;

namespace Infrastructure;

public interface IAddressableDictionary<TKey, TValue> : IGrainWithGuidKey
    where TKey : notnull

{
    Task<IReadOnlyDictionary<TKey, TValue>> GetAll();
}

[GenerateSerializer]
public class AddressableDictionaryState<TKey, TValue> :
    BatchWriterState<AddressableDictionaryCallbacks<TKey, TValue>.IEntry>
    where TKey : notnull
{
    [Id(0)]
    private readonly Dictionary<TKey, TValue> _items = new();

    public IReadOnlyDictionary<TKey, TValue> Items => _items;

    public void Add(TKey key, TValue entry)
    {
        _items[key] = entry;
    }

    public void Remove(TKey key)
    {
        _items.Remove(key);
    }
}

public class AddressableDictionaryCallbacks<TKey, TValue>
{
    public interface IEntry
    {
    }

    [GenerateSerializer]
    public class Add : IEntry
    {
        [Id(0)] public required TKey Key { get; init; }
        [Id(1)] public required TValue Value { get; init; }
    }

    [GenerateSerializer]
    public class Remove : IEntry
    {
        [Id(0)] public required TKey Key { get; init; }
    }
}

[GenerateSerializer]
public class AddressableDictionaryUpdateMessage<TKey, TValue> where TKey : notnull
{
    [Id(0)]
    public Dictionary<TKey, TValue> Updates { get; } = new();

    [Id(1)]
    public List<TKey> Removals { get; } = new();
}

public class AddressableDictionaryMessageQueueId : IMessageQueueId
{
    public AddressableDictionaryMessageQueueId(string name)
    {
        Id = $"addressable-dictionary-update-{name}";
    }

    public string Id { get; }

    public string ToRaw()
    {
        return Id;
    }
}

public abstract class AddressableDictionary<TState, TKey, TValue> :
    BatchWriter<TState, AddressableDictionaryCallbacks<TKey, TValue>.IEntry>,
    IAddressableDictionary<TKey, TValue>
    where TState : AddressableDictionaryState<TKey, TValue>
    where TKey : notnull
{
    public AddressableDictionary(
        IPersistentState<TState> state,
        IMessaging messaging) : base(state)
    {
        _state = state;
        _messaging = messaging;
        _queueId = new AddressableDictionaryMessageQueueId($"{typeof(TKey)}-{typeof(TValue)}");
    }

    private readonly IPersistentState<TState> _state;
    private readonly IMessaging _messaging;
    private readonly AddressableDictionaryMessageQueueId _queueId;

    protected override BatchWriterOptions Options { get; } = new()
    {
        Delay = TimeSpan.FromSeconds(5),
        RequiresTransaction = true
    };

    protected override Task Process(IReadOnlyList<AddressableDictionaryCallbacks<TKey, TValue>.IEntry> entries)
    {
        var update = new AddressableDictionaryUpdateMessage<TKey, TValue>();

        foreach (var entry in entries)
        {
            switch (entry)
            {
                case AddressableDictionaryCallbacks<TKey, TValue>.Add add:
                    _state.State.Add(add.Key, add.Value);
                    update.Updates[add.Key] = add.Value;
                    break;

                case AddressableDictionaryCallbacks<TKey, TValue>.Remove remove:
                    _state.State.Remove(remove.Key);
                    update.Removals.Add(remove.Key);
                    break;
            }
        }

        return _messaging.PushTransactionalQueue(_queueId, update);
    }

    public Task Write(TKey key, TValue value)
    {
        var entry = new AddressableDictionaryCallbacks<TKey, TValue>.Add
        {
            Key = key,
            Value = value
        };

        return WriteTransactional(entry);
    }

    public Task Erase(TKey key)
    {
        var entry = new AddressableDictionaryCallbacks<TKey, TValue>.Remove
        {
            Key = key
        };

        return WriteTransactional(entry);
    }

    public Task<IReadOnlyDictionary<TKey, TValue>> GetAll()
    {
        return Task.FromResult(_state.State.Items);
    }
}