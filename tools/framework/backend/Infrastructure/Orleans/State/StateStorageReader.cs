using System.Text;
using Common.Extensions;
using Npgsql;
using Shared;

namespace Infrastructure.State;

public interface IStateStorageReader
{
    // Event-sourced states come from the Marten snapshots, the rest from the grain state table. orderByProperty
    // sorts only the event-sourced ones: the direct storage is always sorted by key.
    Task<StatePageResult<TKey, TValue>> ReadPage<TKey, TValue>(
        int offset,
        int limit,
        string? orderByProperty = null,
        bool descending = true)
        where TKey : notnull
        where TValue : class, IStateValue, new();
}

public class StateStorageReader : IStateStorageReader
{
    public StateStorageReader(
        IGrainStatesRegistry statesRegistry,
        IEventStorage eventStorage,
        IDbSource dbSource,
        IStateSerializer serializer,
        IStateMigrations migrations)
    {
        _statesRegistry = statesRegistry;
        _eventStorage = eventStorage;
        _dbSource = dbSource;
        _serializer = serializer;
        _migrations = migrations;
    }

    private readonly IGrainStatesRegistry _statesRegistry;
    private readonly IEventStorage _eventStorage;
    private readonly IDbSource _dbSource;
    private readonly IStateSerializer _serializer;
    private readonly IStateMigrations _migrations;

    public async Task<StatePageResult<TKey, TValue>> ReadPage<TKey, TValue>(
        int offset,
        int limit,
        string? orderByProperty = null,
        bool descending = true)
        where TKey : notnull
        where TValue : class, IStateValue, new()
    {
        var stateInfo = _statesRegistry.Get<TValue>();
        var latestVersion = _migrations.GetLatestVersion<TValue>();

        // Event-sourced states are stored not in the grain state table but in Marten snapshots.
        if (typeof(TValue).IsAssignableTo(typeof(IEventStateValue)))
        {
            return await _eventStorage.ReadPage<TKey, TValue>(
                stateInfo.Name + ":",
                stateInfo.KeyType,
                offset,
                limit,
                orderByProperty,
                descending);
        }

        await using var connection = await _dbSource.Value.OpenConnectionAsync();

        await using var countCmd = connection.CreateCommand();

        countCmd.CommandText =
            $"SELECT COUNT(*)::int FROM {stateInfo.TableName} WHERE type = @type AND value IS NOT NULL";
        countCmd.Parameters.AddWithValue("type", stateInfo.Name);
        var totalCount = (int)(await countCmd.ExecuteScalarAsync()).ThrowIfNull();

        await using var cmd = connection.CreateCommand();

        cmd.CommandText =
            $"SELECT key, value, version FROM {stateInfo.TableName} WHERE type = @type AND value IS NOT NULL ORDER BY key DESC OFFSET @offset LIMIT @limit";
        cmd.Parameters.AddWithValue("type", stateInfo.Name);
        cmd.Parameters.AddWithValue("offset", offset);
        cmd.Parameters.AddWithValue("limit", limit);

        var entries = new List<(TKey Key, TValue Value)>();
        await using var reader = await cmd.ExecuteReaderAsync();

        // A row that does not read throws: a skipped one would still be counted by TotalCount.
        while (await reader.ReadAsync())
        {
            var key = ReadKeyFromReader<TKey>(reader, stateInfo);

            var payloadBinary = reader.GetFieldValue<byte[]>(1);
            var version = reader.GetFieldValue<int>(2);

            var startIndex = payloadBinary[0] == 0x01 ? 1 : 0;
            var raw = Encoding.UTF8.GetString(payloadBinary, startIndex, payloadBinary.Length - startIndex);

            var value = version < latestVersion
                ? _migrations.Migrate<TValue>(raw, version)
                : _serializer.Deserialize<TValue>(raw);

            entries.Add((key, value));
        }

        return new StatePageResult<TKey, TValue>
        {
            Entries = entries,
            TotalCount = totalCount
        };
    }

    private static TKey ReadKeyFromReader<TKey>(NpgsqlDataReader reader, GrainStateInfo info) => info.KeyType switch
    {
        GrainKeyType.Guid or GrainKeyType.GuidAndString => (TKey)(object)reader.GetFieldValue<Guid>(0),
        GrainKeyType.String => (TKey)(object)reader.GetFieldValue<string>(0),
        GrainKeyType.Integer or GrainKeyType.IntegerAndString => (TKey)(object)reader.GetFieldValue<long>(0),
        _ => throw new InvalidOperationException($"[StateStorageReader] Unsupported key type: {info.KeyType}")
    };
}
