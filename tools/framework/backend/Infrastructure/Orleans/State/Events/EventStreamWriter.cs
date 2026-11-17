using Common.Extensions;
using System.Data.Common;
using Marten;
using Marten.Services;
using Npgsql;

namespace Infrastructure.State;

// The changes of the event streams: appends and archives, each in its own session or in the transaction of the caller.
internal class EventStreamWriter
{
    public EventStreamWriter(IDocumentStore store, IStateSerializer stateSerializer)
    {
        _store = store;
        _stateSerializer = stateSerializer;
    }

    private readonly IDocumentStore _store;
    private readonly IStateSerializer _stateSerializer;

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

    private object Deserialize(EventPayload payload)
    {
        // As the states: an event type moved to another assembly with its namespace kept is still found.
        var type = MovedTypesBinder.Resolve(payload.Type);

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
}
