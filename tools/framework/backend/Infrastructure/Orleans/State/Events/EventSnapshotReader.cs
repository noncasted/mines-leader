using System.Linq.Expressions;
using Marten;
using Shared;

namespace Infrastructure.State;

// The reads of the event states: the inline snapshots of the streams, by id and by the prefix of the stream.
internal class EventSnapshotReader
{
    public EventSnapshotReader(IDocumentStore store)
    {
        _store = store;
    }

    private readonly IDocumentStore _store;

    public async Task<T> Read<T>(string streamId) where T : class, IEventStateValue, new()
    {
        await using var session = _store.QuerySession();

        // Snapshot is an inline projection (see MartenSetupExtensions): missing snapshot = missing stream,
        // so no second round trip to aggregate mt_events for a new stream.
        var aggregate = await TryLoad<T>(session, streamId);

        if (aggregate == null)
            return new T();

        RestoredId(aggregate, id => id == streamId, $"the stream id '{streamId}'");

        return aggregate;
    }

    public async Task<T> Rebuild<T>(string streamId) where T : class, IEventStateValue, new()
    {
        await using var session = _store.QuerySession();

        var aggregate = await session.Events.AggregateStreamAsync<T>(streamId);

        return aggregate ?? new T();
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
        var requested = streamIds.ToHashSet();

        foreach (var aggregate in aggregates)
        {
            var streamId = RestoredId(aggregate, requested.Contains, "one of the requested stream ids");
            result[EventStreamKeys.ParseStream<TKey>(streamId)] = aggregate;
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

        var aggregates = await session.Query<TValue>()
                                      .Where(BuildPrefixPredicate<TValue>(streamPrefix))
                                      .ToListAsync();

        foreach (var aggregate in aggregates)
        {
            var id = RestoredIdByPrefix(aggregate, streamPrefix);
            var key = EventStreamKeys.Parse<TKey>(EventStreamKeys.GrainKey(id), keyType);
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

        // An aggregate whose stream id does not map to the key is broken data: it throws, it is not left out of the
        // page (the total count would still include it).
        foreach (var aggregate in aggregates)
        {
            var id = RestoredIdByPrefix(aggregate, streamPrefix);
            var key = EventStreamKeys.Parse<TKey>(EventStreamKeys.GrainKey(id), keyType);
            entries.Add((key, aggregate));
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

        // Sorting by Id is the only one guaranteed to be available: every IEventStateValue has it.
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

    // A snapshot that does not deserialize throws: it is not rebuilt from the events behind the caller's back (Rebuild
    // is the explicit way after a manual recovery).
    private static Task<T?> TryLoad<T>(IQuerySession session, string streamId) where T : class, new()
    {
        return session.LoadAsync<T>(streamId);
    }

    private static string RestoredIdByPrefix(object aggregate, string streamPrefix)
    {
        return RestoredId(
            aggregate,
            id => id.StartsWith(streamPrefix, StringComparison.Ordinal),
            $"a stream id with the prefix '{streamPrefix}'");
    }

    // Marten keeps the stream id in the id column and writes it into the json of the snapshot; a loaded snapshot takes Id
    // from the json alone. An Id the serializer can't restore means an empty Id on every read and a snapshot Marten
    // restarts from each new event: the state is stale with no error of its own, so the read throws.
    private static string RestoredId(object aggregate, Func<string, bool> isExpected, string expected)
    {
        var id = ((IEventStateValue)aggregate).Id;

        if (isExpected(id))
            return id;

        throw new InvalidOperationException(
            $"[EventStorage] The snapshot of {aggregate.GetType().Name} is read with Id '{id}' instead of {expected}. " +
            "An Id with a non-public setter needs [JsonProperty], otherwise the serializer does not restore it " +
            "and the snapshot is stale.");
    }
}
