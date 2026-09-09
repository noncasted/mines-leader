using System.Data.Common;
using System.Linq.Expressions;
using Common;
using Marten;
using Marten.Services;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Npgsql;
using Shared;

namespace Infrastructure.State;

[GenerateSerializer]
public class GrainEventRecord
{
    [Id(0)]
    public required string StreamId { get; init; }

    [Id(1)]
    public required List<EventPayload> Events { get; init; }
}

public interface IEventStorage
{
    Task<T> Read<T>(string streamId) where T : class, IEventStateValue, new();

    // Explicit aggregation over mt_events, bypassing the inline snapshot. Not on the hot path:
    // the snapshot is an inline projection updated in the same transaction as every Append,
    // so Read trusts it. Use Rebuild only when the snapshot may be stale (manual DB recovery).
    Task<T> Rebuild<T>(string streamId) where T : class, IEventStateValue, new();

    Task Append(string streamId, params object[] events);
    Task Write(DbTransaction transaction, IReadOnlyList<GrainEventRecord> records);
    Task Delete(string streamId);
    Task DeleteBatch(IReadOnlyList<string> streamIds);
    Task DeleteBatch(NpgsqlTransaction transaction, IReadOnlyList<string> streamIds);

    Task<IReadOnlyDictionary<TKey, TValue>> ReadBatch<TKey, TValue>(IReadOnlyList<string> streamIds)
        where TKey : notnull
        where TValue : class, new();

    IAsyncEnumerable<(TKey Key, TValue Aggregate)> ReadAll<TKey, TValue>(string streamPrefix, GrainKeyType keyType)
        where TKey : notnull
        where TValue : class, new();

    Task<StatePageResult<TKey, TValue>> ReadPage<TKey, TValue>(
        string streamPrefix,
        GrainKeyType keyType,
        int offset,
        int limit,
        string? orderByProperty = null,
        bool descending = true)
        where TKey : notnull
        where TValue : class, new();
}

public class EventStorage : IEventStorage
{
    private readonly IDocumentStore _store;
    private readonly IStateSerializer _stateSerializer;
    private readonly ILogger<EventStorage> _logger;

    public EventStorage(IDocumentStore store, IStateSerializer stateSerializer, ILogger<EventStorage> logger)
    {
        _store = store;
        _stateSerializer = stateSerializer;
        _logger = logger;
    }

    public async Task<T> Read<T>(string streamId) where T : class, IEventStateValue, new()
    {
        await using var session = _store.QuerySession();

        // Snapshot is an inline projection (see MartenSetupExtensions): missing snapshot = missing stream,
        // so no second round trip to aggregate mt_events for a new stream.
        var aggregate = await TryLoadAsync<T>(session, streamId);

        if (aggregate == null)
            return new T();

        SetIdFromStream(aggregate, streamId);
        return aggregate;
    }

    public async Task<T> Rebuild<T>(string streamId) where T : class, IEventStateValue, new()
    {
        await using var session = _store.QuerySession();

        var aggregate = await session.Events.AggregateStreamAsync<T>(streamId);

        if (aggregate == null)
            return new T();

        SetIdFromStream(aggregate, streamId);
        return aggregate;
    }

    public async Task Append(string streamId, params object[] events)
    {
        await using var session = _store.LightweightSession();
        session.Events.Append(streamId, events);
        await session.SaveChangesAsync();
    }

    public async Task Write(DbTransaction transaction, IReadOnlyList<GrainEventRecord> records)
    {
        var npgsqlTransaction = (NpgsqlTransaction)transaction;

        await using var session =
            _store.OpenSession(SessionOptions.ForTransaction(npgsqlTransaction, shouldAutoCommit: false));

        foreach (var record in records)
        {
            var events = record.Events
                               .Select(Deserialize)
                               .ToArray();

            if (events.Length != 0)
                session.Events.Append(record.StreamId, events);
        }

        await session.SaveChangesAsync();
    }

    public async Task Delete(string streamId)
    {
        await using var session = _store.LightweightSession();
        session.Events.ArchiveStream(streamId);
        await session.SaveChangesAsync();
    }

    public async Task DeleteBatch(IReadOnlyList<string> streamIds)
    {
        if (streamIds.Count == 0)
            return;

        const int batchSize = 1000;

        for (var i = 0; i < streamIds.Count; i += batchSize)
        {
            var batch = streamIds.Skip(i).Take(batchSize).ToList();
            await using var session = _store.LightweightSession();

            foreach (var streamId in batch)
                session.Events.ArchiveStream(streamId);

            await session.SaveChangesAsync();
        }
    }

    public async Task DeleteBatch(NpgsqlTransaction transaction, IReadOnlyList<string> streamIds)
    {
        if (streamIds.Count == 0)
            return;

        const int batchSize = 1000;

        for (var i = 0; i < streamIds.Count; i += batchSize)
        {
            var batch = streamIds.Skip(i).Take(batchSize).ToList();

            await using var session =
                _store.OpenSession(SessionOptions.ForTransaction(transaction, shouldAutoCommit: false));

            foreach (var streamId in batch)
                session.Events.ArchiveStream(streamId);

            await session.SaveChangesAsync();
        }
    }

    public async Task<IReadOnlyDictionary<TKey, TValue>> ReadBatch<TKey, TValue>(IReadOnlyList<string> streamIds)
        where TKey : notnull
        where TValue : class, new()
    {
        if (streamIds.Count == 0)
            return new Dictionary<TKey, TValue>();

        var result = new Dictionary<TKey, TValue>(streamIds.Count);

        await using var session = _store.QuerySession();

        // One query for all snapshots. Missing snapshot = missing stream (same rule as Read).
        var aggregates = await session.LoadManyAsync<TValue>(streamIds);

        foreach (var aggregate in aggregates)
        {
            if (aggregate is not IEventStateValue esv)
                continue;

            var streamId = esv.Id;
            SetIdFromStream(aggregate, streamId);
            result[ParseStreamKey<TKey>(streamId)] = aggregate;
        }

        return result;
    }

	    public async IAsyncEnumerable<(TKey Key, TValue Aggregate)> ReadAll<TKey, TValue>(
	        string streamPrefix,
	        GrainKeyType keyType)
	        where TKey : notnull
	        where TValue : class, new()
	    {
	        await using var session = _store.QuerySession();

	        var parameter = Expression.Parameter(typeof(TValue), "x");
	        var idProperty = Expression.Property(parameter, "Id");
	        var startsWithMethod = typeof(string).GetMethod("StartsWith", new[] { typeof(string) })!;
	        var body = Expression.Call(idProperty, startsWithMethod, Expression.Constant(streamPrefix));
	        var predicate = Expression.Lambda<Func<TValue, bool>>(body, parameter);

	        var aggregates = await session.Query<TValue>()
	            .Where(predicate)
	            .ToListAsync();

	        foreach (var aggregate in aggregates)
	        {
	            var id = ((IEventStateValue)aggregate).Id;
	            var grainKey = ExtractGrainKey(id);
	            var key = ParseKey<TKey>(grainKey, keyType);
	            yield return (key, aggregate);
	        }
	    }

    public async Task<StatePageResult<TKey, TValue>> ReadPage<TKey, TValue>(
        string streamPrefix,
        GrainKeyType keyType,
        int offset,
        int limit,
        string? orderByProperty = null,
        bool descending = true)
        where TKey : notnull
        where TValue : class, new()
    {
        await using var session = _store.QuerySession();

        var query = session.Query<TValue>().Where(BuildPrefixPredicate<TValue>(streamPrefix));

        var totalCount = await query.CountAsync();

        var ordered = ApplyOrder(query, orderByProperty, descending);

        var aggregates = await ordered.Skip(offset).Take(limit).ToListAsync();

        var entries = new List<(TKey Key, TValue Value)>(aggregates.Count);

        foreach (var aggregate in aggregates)
        {
            try
            {
                var id = ((IEventStateValue)aggregate).Id;
                var key = ParseKey<TKey>(ExtractGrainKey(id), keyType);
                entries.Add((key, aggregate));
            }
            catch (Exception e)
            {
                _logger.LogWarning(e, "[EventStorage] Failed to map aggregate {Type}, skipping", typeof(TValue).Name);
            }
        }

        return new StatePageResult<TKey, TValue>
        {
            Entries = entries,
            TotalCount = totalCount
        };
    }

    private static Expression<Func<TValue, bool>> BuildPrefixPredicate<TValue>(string streamPrefix)
    {
        var parameter = Expression.Parameter(typeof(TValue), "x");
        var idProperty = Expression.Property(parameter, "Id");
        var startsWithMethod = typeof(string).GetMethod("StartsWith", new[] { typeof(string) })!;
        var body = Expression.Call(idProperty, startsWithMethod, Expression.Constant(streamPrefix));
        return Expression.Lambda<Func<TValue, bool>>(body, parameter);
    }

    private static IQueryable<TValue> ApplyOrder<TValue>(IQueryable<TValue> query, string? property, bool descending)
    {
        var parameter = Expression.Parameter(typeof(TValue), "x");

        // Сортировка по Id — единственная гарантированно доступная: она есть у любого IEventStateValue.
        var member = property != null && typeof(TValue).GetProperty(property) != null
            ? Expression.Property(parameter, property)
            : Expression.Property(parameter, "Id");

        var lambda = Expression.Lambda(member, parameter);
        var method = descending ? "OrderByDescending" : "OrderBy";

        var call = Expression.Call(
            typeof(Queryable),
            method,
            [typeof(TValue), member.Type],
            query.Expression,
            Expression.Quote(lambda));

        return query.Provider.CreateQuery<TValue>(call);
    }

    private object Deserialize(EventPayload payload)
    {
        var type = Type.GetType(payload.Type) ??
                   throw new InvalidOperationException(
                       $"Unable to resolve event type '{payload.Type}'. Ensure the type is available in the current AppDomain.");

        try
        {
            return _stateSerializer.Deserialize(payload.Json, type) ??
                   throw new InvalidOperationException(
                       $"Event deserialization returned null. Type: {type.Name}, Json: {payload.Json}");
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to deserialize event of type '{type.Name}'. Json: {payload.Json}", ex);
        }
    }

    private static string ExtractGrainKey(string streamId)
    {
        var colonIndex = streamId.IndexOf(':');
        return colonIndex >= 0 ? streamId.Substring(colonIndex + 1) : streamId;
    }

    private static void SetIdFromStream<T>(T aggregate, string streamId) where T : class
    {
        if (aggregate is IEventStateValue esv)
            esv.Id = streamId;
    }

    private static async Task<T?> TryLoadAsync<T>(IQuerySession session, string streamId) where T : class, new()
    {
        try
        {
            return await session.LoadAsync<T>(streamId);
        }
        catch (JsonException)
        {
            return await session.Events.AggregateStreamAsync<T>(streamId);
        }
    }

    private static TKey ParseKey<TKey>(string value, GrainKeyType keyType) => keyType switch
    {
        GrainKeyType.Guid or GrainKeyType.GuidAndString => (TKey)(object)Guid.Parse(value),
        GrainKeyType.String => (TKey)(object)value,
        GrainKeyType.Integer or GrainKeyType.IntegerAndString => (TKey)(object)long.Parse(value),
        _ => throw new InvalidOperationException($"[EventStorage] Unsupported key type: {keyType}")
    };

    private static TKey ParseStreamKey<TKey>(string streamId)
    {
        var grainKey = ExtractGrainKey(streamId);

        if (typeof(TKey) == typeof(Guid))
            return (TKey)(object)Guid.Parse(grainKey);

        if (typeof(TKey) == typeof(string))
            return (TKey)(object)grainKey;

        if (typeof(TKey) == typeof(long))
            return (TKey)(object)long.Parse(grainKey);

        throw new InvalidOperationException($"[EventStorage] Unsupported batch key type: {typeof(TKey).Name}");
    }
}
