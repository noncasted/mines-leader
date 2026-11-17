using System.Data.Common;
using Marten;
using Npgsql;
using Shared;

namespace Infrastructure.State;

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
    public EventStorage(IDocumentStore store, IStateSerializer stateSerializer)
    {
        _reader = new EventSnapshotReader(store);
        _writer = new EventStreamWriter(store, stateSerializer);
    }

    private readonly EventSnapshotReader _reader;
    private readonly EventStreamWriter _writer;

    public Task<T> Read<T>(string streamId) where T : class, IEventStateValue, new() => _reader.Read<T>(streamId);

    public Task<T> Rebuild<T>(string streamId) where T : class, IEventStateValue, new() => _reader.Rebuild<T>(streamId);

    public Task Append(string streamId, params object[] events) => _writer.Append(streamId, events);

    public Task Write(DbTransaction transaction, IReadOnlyList<GrainEventRecord> records) =>
        _writer.Write(transaction, records);

    public Task Delete(string streamId) => _writer.Delete(streamId);

    public Task DeleteBatch(IReadOnlyList<string> streamIds) => _writer.DeleteBatch(streamIds);

    public Task DeleteBatch(NpgsqlTransaction transaction, IReadOnlyList<string> streamIds) =>
        _writer.DeleteBatch(transaction, streamIds);

    public Task<IReadOnlyDictionary<TKey, TValue>> ReadBatch<TKey, TValue>(IReadOnlyList<string> streamIds)
        where TKey : notnull
        where TValue : class, new()
    {
        return _reader.ReadBatch<TKey, TValue>(streamIds);
    }

    public IAsyncEnumerable<(TKey Key, TValue Aggregate)> ReadAll<TKey, TValue>(string streamPrefix, GrainKeyType keyType)
        where TKey : notnull
        where TValue : class, new()
    {
        return _reader.ReadAll<TKey, TValue>(streamPrefix, keyType);
    }

    public Task<StatePageResult<TKey, TValue>> ReadPage<TKey, TValue>(
        string streamPrefix,
        GrainKeyType keyType,
        int offset,
        int limit,
        string? orderByProperty = null,
        bool descending = true)
        where TKey : notnull
        where TValue : class, new()
    {
        return _reader.ReadPage<TKey, TValue>(streamPrefix, keyType, offset, limit, orderByProperty, descending);
    }
}
