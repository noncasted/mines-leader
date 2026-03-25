using System.Text;
using Common.Extensions;
using Microsoft.Extensions.Logging;
using Npgsql;
using NpgsqlTypes;

namespace Infrastructure.State;

public class StateIdentity
{
    public required object Key { get; init; }
    public required string Type { get; init; }
    public required string? Extension { get; init; }
}

public class GrainStateRecord
{
    public required GrainId Id { get; init; }
    public required IStateValue Value { get; init; }
}

public interface IStateStorage
{
    IGrainStatesRegistry Registry { get; }

    Task<T> Read<T>(StateIdentity stateIdentity) where T : IStateValue, new();
    Task<(string, int)> ReadRaw<T>(StateIdentity stateIdentity) where T : IStateValue, new();
    Task Write(StateIdentity identity, IStateValue value);
    Task Write(NpgsqlTransaction transaction, IReadOnlyDictionary<StateIdentity, IStateValue> records);
}

public class StateStorage : IStateStorage
{
    public StateStorage(
        IGrainStatesRegistry statesRegistry,
        IDbSource dbSource,
        IStateSerializer serializer,
        IStateMigrations migrations,
        ILogger<StateStorage> logger)
    {
        _dbSource = dbSource;
        _serializer = serializer;
        _migrations = migrations;
        _logger = logger;
        _cache = new StateStorageCache(statesRegistry);
        Registry = statesRegistry;
    }

    private readonly IDbSource _dbSource;
    private readonly IStateSerializer _serializer;
    private readonly IStateMigrations _migrations;
    private readonly ILogger<StateStorage> _logger;
    private readonly StateStorageCache _cache;

    public IGrainStatesRegistry Registry { get; }

    public async Task<T> Read<T>(StateIdentity stateIdentity) where T : IStateValue, new()
    {
        try
        {
            var (raw, version) = await ReadRaw<T>(stateIdentity);

            if (raw == string.Empty)
                return new T();

            var latestVersion = _migrations.GetLatestVersion<T>();

            if (version < latestVersion)
                return _migrations.Migrate<T>(raw, version);

            return _serializer.TryDeserialize<T>(raw)!;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[StateStorage] Failed to read {Type} key={Key} type={StateType}",
                typeof(T).Name, stateIdentity.Key, stateIdentity.Type
            );
            
            throw;
        }
    }

    public async Task<(string, int)> ReadRaw<T>(StateIdentity stateIdentity) where T : IStateValue, new()
    {
        try
        {
            await using var connection = await _dbSource.Value.OpenConnectionAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = _cache.GetReadQuery<T>(stateIdentity.Extension != null);

            command.Parameters.AddWithValue("type", stateIdentity.Type);
            command.Parameters.AddWithValue("key", stateIdentity.Key);

            if (stateIdentity.Extension != null)
                command.Parameters.AddWithValue("extension", stateIdentity.Extension);

            await using var reader = await command.ExecuteReaderAsync();

            if (await reader.ReadAsync() == false)
                return (string.Empty, -1);

            var payloadBinary = reader.GetFieldValue<byte[]>(0);
            var version = reader.GetFieldValue<int>(1);

            if (payloadBinary == null || payloadBinary.Length == 0)
                throw new Exception();

            var startIndex = payloadBinary[0] == 0x01 ? 1 : 0;
            var raw = Encoding.UTF8.GetString(payloadBinary, startIndex, payloadBinary.Length - startIndex);

            return (raw, version);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[StateStorage] Failed to read raw {Type} key={Key} type={StateType}",
                typeof(T).Name, stateIdentity.Key, stateIdentity.Type
            );
            
            throw;
        }
    }

    public async Task Write(StateIdentity identity, IStateValue value)
    {
        await using var connection = await _dbSource.Value.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        try
        {
            await Write(transaction, new Dictionary<StateIdentity, IStateValue>
                {
                    { identity, value }
                }
            );

            await transaction.CommitAsync();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[StateStorage] Failed to write {Type} key={Key} type={StateType}",
                value.GetType().Name, identity.Key, identity.Type
            );
            await transaction.RollbackAsync();
            
            throw;
        }
    }

    public async Task Write(NpgsqlTransaction transaction, IReadOnlyDictionary<StateIdentity, IStateValue> records)
    {
        foreach (var (identity, value) in records)
        {
            try
            {
                var json = _serializer.Serialize(value);

                await using var command = transaction.Connection!.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = _cache.GetWriteQuery(value.GetType(), identity.Extension != null);

                command.Parameters.AddWithValue("type", identity.Type);
                command.Parameters.AddWithValue("key", identity.Key);
                command.Parameters.AddWithValue("version", value.Version);

                if (identity.Extension != null)
                    command.Parameters.AddWithValue("extension", identity.Extension);

                var valueParameter = command.Parameters.AddWithValue("@value", json);
                valueParameter.NpgsqlDbType = NpgsqlDbType.Jsonb;

                await command.ExecuteNonQueryAsync();
            }
            catch (Exception e)
            {
                _logger.LogError(e, "[StateStorage] Failed to write record {Type} key={Key} type={StateType}",
                    value.GetType().Name, identity.Key, identity.Type
                );

                throw;
            }
        }
    }
}