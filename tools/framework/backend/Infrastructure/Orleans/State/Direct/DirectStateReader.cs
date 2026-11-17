using System.Text;
using Common;
using Common.Extensions;
using Internal;
using Microsoft.Extensions.Logging;
using Npgsql;
using Shared;

namespace Infrastructure.State;

// The reads of the direct states: a stored value, migrated to the latest version when it is older.
internal class DirectStateReader
{
    public DirectStateReader(
        IGrainStatesRegistry registry,
        IDbSource dbSource,
        IStateSerializer serializer,
        IStateMigrations migrations,
        ILogger logger)
    {
        _registry = registry;
        _dbSource = dbSource;
        _serializer = serializer;
        _migrations = migrations;
        _logger = logger;
        _cache = new StateStorageCache();
    }

    private readonly IGrainStatesRegistry _registry;
    private readonly IDbSource _dbSource;
    private readonly IStateSerializer _serializer;
    private readonly IStateMigrations _migrations;
    private readonly ILogger _logger;
    private readonly StateStorageCache _cache;

    public async Task<T> Read<T>(StateIdentity identity) where T : IStateValue, new()
    {
        using var watch = MetricWatch.Start(StateTelemetry.ReadDuration);

        try
        {
            var (raw, version) = await ReadRaw(identity);

            if (raw == string.Empty)
                return new T();

            var latestVersion = _migrations.GetLatestVersion<T>();

            if (version < latestVersion)
                return _migrations.Migrate<T>(raw, version);

            return _serializer.TryDeserialize<T>(raw).ThrowIfNull();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[DirectStorage] Failed to read {Type} key={Key} type={StateType}",
                typeof(T).Name, identity.Key, identity.Type);

            throw;
        }
        finally
        {
            StateTelemetry.ReadTotal.Add(1);
        }
    }

    public async Task<IReadOnlyDictionary<TKey, TValue>> ReadBatch<TKey, TValue>(
        IReadOnlyList<StateIdentity> identities)
        where TKey : notnull
        where TValue : class, IStateValue, new()
    {
        if (identities.Count == 0)
            return new Dictionary<TKey, TValue>();

        var stateInfo = _registry.Get<TValue>();
        var latestVersion = _migrations.GetLatestVersion<TValue>();
        var result = new Dictionary<TKey, TValue>(identities.Count);

        try
        {
            await using var connection = await _dbSource.Value.OpenConnectionAsync();

            foreach (var group in identities.GroupBy(i => i.Extension))
            {
                var groupList = group.ToList();
                var hasExtension = group.Key != null;

                await using var command = connection.CreateCommand();
                command.CommandText = _cache.GetReadBatchQuery(groupList.First());
                command.Parameters.AddWithValue("type", stateInfo.Name);
                command.Parameters.AddWithValue("keys", BuildKeysArray(groupList, stateInfo.KeyType));

                if (hasExtension)
                    command.Parameters.AddWithValue("extension", group.Key!);

                await using var reader = await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    var key = ReadKeyFromReader<TKey>(reader, stateInfo);

                    var payloadBinary = reader.GetFieldValue<byte[]>(1);
                    var version = reader.GetFieldValue<int>(2);

                    var raw = DecodePayload(payloadBinary);

                    result[key] = version < latestVersion
                        ? _migrations.Migrate<TValue>(raw, version)
                        : _serializer.TryDeserialize<TValue>(raw).ThrowIfNull();
                }
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[DirectStorage] Failed to batch read {Type} count={Count}",
                typeof(TValue).Name, identities.Count);

            throw;
        }

        return result;
    }

    public async IAsyncEnumerable<(TKey, TValue)> ReadAll<TKey, TValue>(IReadOnlyLifetime lifetime)
        where TValue : IStateValue, new()
    {
        var stateInfo = _registry.Get<TValue>();
        var cancellation = lifetime.Token;
        var latestVersion = _migrations.GetLatestVersion<TValue>();

        await using var connection = await _dbSource.Value.OpenConnectionAsync(cancellation);
        await using var command = connection.CreateCommand();

        var identity = new StateIdentity
        {
            Key = null!,
            Type = stateInfo.Name,
            TableName = stateInfo.TableName,
            Extension = null
        };

        command.CommandText = _cache.GetReadAllQuery(identity);
        command.Parameters.AddWithValue("type", stateInfo.Name);

        await using var reader = await command.ExecuteReaderAsync(cancellation);

        while (await reader.ReadAsync(cancellation))
        {
            var key = ReadKeyFromReader<TKey>(reader, stateInfo);

            var payloadBinary = reader.GetFieldValue<byte[]>(1);
            var version = reader.GetFieldValue<int>(2);

            var raw = DecodePayload(payloadBinary);

            var value = version < latestVersion
                ? _migrations.Migrate<TValue>(raw, version)
                : _serializer.TryDeserialize<TValue>(raw).ThrowIfNull();

            yield return (key, value);
        }
    }

    public async IAsyncEnumerable<TKey> ReadAllKeys<TKey>(GrainStateInfo stateInfo, IReadOnlyLifetime lifetime)
    {
        var cancellation = lifetime.Token;

        await using var connection = await _dbSource.Value.OpenConnectionAsync(cancellation);
        await using var command = connection.CreateCommand();

        command.CommandText = $"SELECT key FROM {stateInfo.TableName} WHERE type = @type";
        command.Parameters.AddWithValue("type", stateInfo.Name);

        await using var reader = await command.ExecuteReaderAsync(cancellation);

        while (await reader.ReadAsync(cancellation))
            yield return ReadKeyFromReader<TKey>(reader, stateInfo);
    }

    public async Task<string> ReadRawJson(StateIdentity identity)
    {
        var (raw, _) = await ReadRaw(identity);
        return raw;
    }

    private async Task<(string, int)> ReadRaw(StateIdentity stateIdentity)
    {
        try
        {
            await using var connection = await _dbSource.Value.OpenConnectionAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = _cache.GetReadQuery(stateIdentity);

            command.Parameters.AddWithValue("type", stateIdentity.Type);
            command.Parameters.AddWithValue("key", stateIdentity.Key);

            if (stateIdentity.Extension != null)
                command.Parameters.AddWithValue("extension", stateIdentity.Extension);

            await using var reader = await command.ExecuteReaderAsync();

            if (await reader.ReadAsync() == false)
                return (string.Empty, -1);

            var payloadBinary = reader.GetFieldValue<byte[]>(0);
            var version = reader.GetFieldValue<int>(1);

            var raw = DecodePayload(payloadBinary);

            return (raw, version);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[DirectStorage] Failed to read raw key={Key} type={StateType}",
                stateIdentity.Key, stateIdentity.Type);

            throw;
        }
    }

    private static string DecodePayload(byte[] payloadBinary)
    {
        var startIndex = payloadBinary[0] == 0x01 ? 1 : 0;
        return Encoding.UTF8.GetString(payloadBinary, startIndex, payloadBinary.Length - startIndex);
    }

    private static TKey ReadKeyFromReader<TKey>(NpgsqlDataReader reader, GrainStateInfo info) => info.KeyType switch
    {
        GrainKeyType.Guid or GrainKeyType.GuidAndString => (TKey)(object)reader.GetFieldValue<Guid>(0),
        GrainKeyType.String => (TKey)(object)reader.GetFieldValue<string>(0),
        GrainKeyType.Integer or GrainKeyType.IntegerAndString => (TKey)(object)reader.GetFieldValue<long>(0),
        _ => throw new InvalidOperationException($"[DirectStorage] Unsupported key type: {info.KeyType}")
    };

    private static object BuildKeysArray(IList<StateIdentity> identities, GrainKeyType keyType) => keyType switch
    {
        GrainKeyType.Guid or GrainKeyType.GuidAndString => identities.Select(i => (Guid)i.Key).ToArray(),
        GrainKeyType.String => identities.Select(i => (string)i.Key).ToArray(),
        GrainKeyType.Integer or GrainKeyType.IntegerAndString => identities.Select(i => (long)i.Key).ToArray(),
        _ => throw new InvalidOperationException($"[DirectStorage] Unsupported key type: {keyType}")
    };
}
