using System.Data.Common;
using Marten;
using Marten.Services;
using Microsoft.Extensions.Logging;
using Npgsql;

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
    Task<T> Read<T>(string streamId) where T : class, new();
    Task Append(string streamId, params object[] events);
    Task Write(DbTransaction transaction, IReadOnlyList<GrainEventRecord> records);
    IAsyncEnumerable<(string StreamId, T Aggregate)> ReadAll<T>(string streamPrefix) where T : class, new();
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

    public async Task<T> Read<T>(string streamId) where T : class, new()
    {
        await using var session = _store.QuerySession();
        var aggregate = await session.LoadAsync<T>(streamId);
        if (aggregate != null)
            return aggregate;

        aggregate = await session.Events.AggregateStreamAsync<T>(streamId);
        return aggregate ?? new T();
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
                               .Where(e => e != null)
                               .Cast<object>()
                               .ToArray();

            if (events.Length != 0)
                session.Events.Append(record.StreamId, events);
        }

        await session.SaveChangesAsync();
    }

    private object? Deserialize(EventPayload payload)
    {
        var type = Type.GetType(payload.Type);

        if (type == null)
            return null;

        try
        {
            return _stateSerializer.Deserialize(payload.Json, type);
        }
        catch
        {
            return null;
        }
    }

    public async IAsyncEnumerable<(string StreamId, T Aggregate)> ReadAll<T>(string streamPrefix)
        where T : class, new()
    {
        await using var session = _store.QuerySession();

        var aggregates = await session.Query<T>()
            .Where(x => ((IEventStateValue)x).Id.StartsWith(streamPrefix))
            .ToListAsync();

        foreach (var aggregate in aggregates)
        {
            var id = ((IEventStateValue)aggregate).Id;
            yield return (id, aggregate);
        }
    }
}
