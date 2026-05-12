using Common;
using Common.Reactive;
using Marten;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Infrastructure.State;

public class StateIdentity
{
    public required object Key { get; init; }
    public required string Type { get; init; }
    public required string TableName { get; init; }
    public required string? Extension { get; init; }
}

public class GrainStateRecord
{
    public required GrainId Id { get; init; }
    public required IStateValue Value { get; init; }
}

public class StateWriteRequest
{
    public required IReadOnlyDictionary<StateIdentity, IStateValue> Records { get; init; }
    public NpgsqlTransaction? Transaction { get; init; }
}

public class StateDeleteRequest
{
    public required IReadOnlyList<StateIdentity> Identities { get; init; }
}

public interface IStateStorage
{
    IGrainStatesRegistry Registry { get; }

    Task<T> Read<T>(StateIdentity identity) where T : IStateValue, new();

    Task<IReadOnlyDictionary<TKey, TValue>> ReadBatch<TKey, TValue>(IReadOnlyList<StateIdentity> identities)
        where TKey : notnull
        where TValue : IStateValue, new();

    IAsyncEnumerable<(TKey, TValue)> ReadAll<TKey, TValue>(IReadOnlyLifetime lifetime)
        where TKey : notnull
        where TValue : class, IStateValue, new();

    Task Write(StateWriteRequest request);
    Task Delete(StateDeleteRequest request);

    Task<string> ReadRawJson(StateIdentity identity);
}

public class StateStorage : IStateStorage
{
    public StateStorage(
        DirectStorage directStorage,
        IEventStorage eventStorage,
        IDocumentStore documentStore,
        IGrainStatesRegistry statesRegistry,
        IStateSerializer stateSerializer,
        ILogger<StateStorage> logger)
    {
        _directStorage = directStorage;
        _eventStorage = eventStorage;
        _documentStore = documentStore;
        _stateSerializer = stateSerializer;
        _logger = logger;
        Registry = statesRegistry;
    }

    private readonly DirectStorage _directStorage;
    private readonly IEventStorage _eventStorage;
    private readonly IDocumentStore _documentStore;
    private readonly IStateSerializer _stateSerializer;
    private readonly ILogger<StateStorage> _logger;

    public IGrainStatesRegistry Registry { get; }

    public Task<T> Read<T>(StateIdentity identity) where T : IStateValue, new()
    {
        if (typeof(T).IsAssignableTo(typeof(IDirectStateValue)))
            return _directStorage.Read<T>(identity);

        if (typeof(T).IsAssignableTo(typeof(IEventStateValue)))
        {
            var streamId = $"{identity.Type}:{identity.Key}";
            var method = typeof(IEventStorage).GetMethod("Read")!.MakeGenericMethod(typeof(T));
            return (Task<T>)method.Invoke(_eventStorage, [streamId])!;
        }

        throw new InvalidOperationException($"State type {typeof(T).Name} must implement either IDirectStateValue or IEventStateValue.");
    }

    public Task<IReadOnlyDictionary<TKey, TValue>> ReadBatch<TKey, TValue>(IReadOnlyList<StateIdentity> identities)
        where TKey : notnull
        where TValue : IStateValue, new()
    {
        if (typeof(TValue).IsAssignableTo(typeof(IDirectStateValue)))
            return _directStorage.ReadBatch<TKey, TValue>(identities);

        if (typeof(TValue).IsAssignableTo(typeof(IEventStateValue)))
            throw new NotSupportedException("Batch read is not supported for event-sourced state values.");

        throw new InvalidOperationException($"State type {typeof(TValue).Name} must implement either IDirectStateValue or IEventStateValue.");
    }

    public IAsyncEnumerable<(TKey, TValue)> ReadAll<TKey, TValue>(IReadOnlyLifetime lifetime)
        where TKey : notnull
        where TValue : class, IStateValue, new()
    {
        if (typeof(TValue).IsAssignableTo(typeof(IDirectStateValue)))
            return _directStorage.ReadAll<TKey, TValue>(lifetime);

        if (typeof(TValue).IsAssignableTo(typeof(IEventStateValue)))
            return ReadAllEventSourced<TKey, TValue>(lifetime);

        throw new InvalidOperationException($"State type {typeof(TValue).Name} must implement either IDirectStateValue or IEventStateValue.");
    }

    private async IAsyncEnumerable<(TKey, TValue)> ReadAllEventSourced<TKey, TValue>(IReadOnlyLifetime lifetime)
        where TKey : notnull
        where TValue : class, IStateValue, new()
    {
        var stateInfo = Registry.Get<TValue>();
        var prefix = stateInfo.Name + ":";

        var aggregates = _eventStorage.ReadAll<TValue>(prefix);

        await foreach (var (streamId, aggregate) in aggregates)
        {
            var grainKey = ExtractGrainKey(streamId);
            var key = ParseKey<TKey>(grainKey, stateInfo.KeyType);
            yield return (key, aggregate);
        }
    }

    public async Task Write(StateWriteRequest request)
    {
        var directRecords = new Dictionary<StateIdentity, IStateValue>();

        foreach (var (identity, value) in request.Records)
        {
            if (value is IDirectStateValue)
            {
                directRecords.Add(identity, value);
            }
            else if (value is IEventStateValue)
            {
                // Event-sourced values are persisted by Marten inline projections.
                // No direct storage write needed.
            }
            else
            {
                throw new InvalidOperationException($"State value type {value.GetType().Name} must implement either IDirectStateValue or IEventStateValue.");
            }
        }

        if (directRecords.Count != 0)
        {
            await _directStorage.Write(new StateWriteRequest
            {
                Records = directRecords,
                Transaction = request.Transaction
            });
        }
    }

    public Task Delete(StateDeleteRequest request)
    {
        // All deletes route through direct storage (event-sourced deletion is stream-based).
        return _directStorage.Delete(request);
    }

    public async Task<string> ReadRawJson(StateIdentity identity)
    {
        var info = Registry.All.FirstOrDefault(s => s.Name == identity.Type);

        if (info != null && typeof(IEventStateValue).IsAssignableFrom(info.Type))
        {
            var streamId = $"{identity.Type}:{identity.Key}";
            var readMethod = typeof(IEventStorage).GetMethod("Read")!.MakeGenericMethod(info.Type);
            var task = (Task)readMethod.Invoke(_eventStorage, [streamId])!;
            await task;
            var resultProperty = task.GetType().GetProperty("Result")!;
            var aggregate = resultProperty.GetValue(task);
            if (aggregate == null)
                return "No state data";
            return _stateSerializer.Serialize(aggregate);
        }

        return await _directStorage.ReadRawJson(identity);
    }

    private static string ExtractGrainKey(string streamId)
    {
        var colonIndex = streamId.IndexOf(':');
        return colonIndex >= 0 ? streamId.Substring(colonIndex + 1) : streamId;
    }

    private static TKey ParseKey<TKey>(string value, GrainKeyType keyType) => keyType switch
    {
        GrainKeyType.Guid or GrainKeyType.GuidAndString => (TKey)(object)Guid.Parse(value),
        GrainKeyType.String => (TKey)(object)value,
        GrainKeyType.Integer or GrainKeyType.IntegerAndString => (TKey)(object)long.Parse(value),
        _ => throw new InvalidOperationException($"[StateStorage] Unsupported key type for ReadAll: {keyType}")
    };
}
