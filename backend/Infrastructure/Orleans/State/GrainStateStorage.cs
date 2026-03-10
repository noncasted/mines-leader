using System.Buffers.Text;
using System.Text;
using Common.Extensions;
using Newtonsoft.Json;
using Npgsql;
using Orleans.Serialization;

namespace Infrastructure.State;

public interface IGrainStateStorage
{
    Task<T> Read<T>(GrainId id) where T : class, new();
    Task<string> ReadRaw<T>(GrainId id) where T : class, new();
    Task Write(GrainId id, object value);
    Task Write(IReadOnlyList<(GrainId id, object value)> states);
}

public class GrainStateStorage : IGrainStateStorage
{
    public GrainStateStorage(
        IGrainStatesRegistry statesRegistry,
        IDbSource dbSource,
        IStateSerializer serializer,
        OrleansJsonSerializer orleansJsonSerializer)
    {
        _statesRegistry = statesRegistry;
        _dbSource = dbSource;
        _serializer = serializer;
        _orleansJsonSerializer = orleansJsonSerializer;
    }

    private readonly IGrainStatesRegistry _statesRegistry;
    private readonly IDbSource _dbSource;
    private readonly IStateSerializer _serializer;
    private readonly OrleansJsonSerializer _orleansJsonSerializer;

    public async Task<T> Read<T>(GrainId id) where T : class, new()
    {
        var raw = await ReadRaw<T>(id);
        return _serializer.TryDeserialize<T>(raw) ?? new T();
    }

    public async Task<string> ReadRaw<T>(GrainId id) where T : class, new()
    {
        var stateInfo = _statesRegistry.States[typeof(T).FullName!];

        await using var connection = await _dbSource.Value.OpenConnectionAsync();
        await using var command = connection.CreateCommand();

        var extension = stateInfo.KeyType is GrainKeyType.GuidAndString or GrainKeyType.IntegerAndString
            ? "and extension = @extension"
            : "";

        var commandText = $@"
            select value
            from {stateInfo.TableName}
            where key = @key
            and type = @type
            {extension}
            ";

        command.CommandText = commandText;

        PassIdentity(command.Parameters, stateInfo, id);

        await using var reader = await command.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
            return JsonConvert.SerializeObject(new T());

        var payloadBinary = reader.GetFieldValue<byte[]>(0);

        if (payloadBinary == null || payloadBinary.Length == 0)
            return "{}";

        var startIndex = payloadBinary[0] == 0x01 ? 1 : 0;
        return Encoding.UTF8.GetString(payloadBinary, startIndex, payloadBinary.Length - startIndex);
    }

    public async Task Write(GrainId id, object value)
    {
        var states = new[] { (id, value) };
        await Write(states);
    }

    public async Task Write(IReadOnlyList<(GrainId id, object value)> states)
    {
        try
        {
            await using var connection = await _dbSource.Value.OpenConnectionAsync();
            await using var transaction = await connection.BeginTransactionAsync();

            foreach (var (grainId, value) in states)
            {
                var stateInfo = _statesRegistry.States[value.GetType().FullName!];
                var json = _serializer.Serialize(value);

                await using var command = connection.CreateCommand();
                command.Transaction = transaction;

                var extension = stateInfo.KeyType is GrainKeyType.GuidAndString or GrainKeyType.IntegerAndString
                    ? ", extension"
                    : "";

                var extensionParam = stateInfo.KeyType is GrainKeyType.GuidAndString or GrainKeyType.IntegerAndString
                    ? ", @extension"
                    : "";

                var commandText = $@"
                    insert into {stateInfo.TableName}
                    (key, type, value{extension})
                    values (@key, @type, @value::jsonb{extensionParam})
                    on conflict (key, type{extension})
                    do update set value = EXCLUDED.value
                ";

                command.CommandText = commandText;

                PassIdentity(command.Parameters, stateInfo, grainId);

                var valueParameter = command.Parameters.AddWithValue("@value", json);
                valueParameter.NpgsqlDbType = NpgsqlTypes.NpgsqlDbType.Jsonb;

                await command.ExecuteNonQueryAsync();
            }

            await transaction.CommitAsync();
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }

    private void PassIdentity(NpgsqlParameterCollection parameter, GrainStateInfo stateInfo, GrainId grainId)
    {
        var span = grainId.Key.AsSpan();

        parameter.AddWithValue("type", stateInfo.Type.FullName!);

        switch (stateInfo.KeyType)
        {
            case GrainKeyType.Integer:
            {
                if (Utf8Parser.TryParse(span, out long key, out _, 'X') == false)
                    throw new Exception($"Failed to parse grain key {grainId} as long.");

                parameter.AddWithValue("key", key);
                break;
            }
            case GrainKeyType.String:
            {
                parameter.AddWithValue("key", grainId.Key.ToString());
                break;
            }
            case GrainKeyType.Guid:
            {
                if (Utf8Parser.TryParse(span, out Guid key, out _, 'N') == false)
                    throw new Exception($"Failed to parse grain key {grainId} as Guid.");

                parameter.AddWithValue("key", key);
                break;
            }
            case GrainKeyType.IntegerAndString:
            {
                var index = span.IndexOf((byte)'+');
                var extension = Encoding.UTF8.GetString(span[(index + 1)..]);
                var keySpan = span[..index];

                if (Utf8Parser.TryParse(keySpan, out long key, out _, 'X') == false)
                    throw new Exception($"Failed to parse grain key {grainId} as long.");

                parameter.AddWithValue("key", key);
                parameter.AddWithValue("extension", extension);
                break;
            }
            case GrainKeyType.GuidAndString:
            {
                var extension = Encoding.UTF8.GetString(span[33..]);
                var keySpan = span[..32];

                if (Utf8Parser.TryParse(keySpan, out Guid key, out _, 'N') == false)
                    throw new Exception($"Failed to parse grain key {grainId} as Guid.");

                parameter.AddWithValue("key", key);
                parameter.AddWithValue("extension", extension);

                break;
            }
            default:
                throw new ArgumentOutOfRangeException();
        }
    }
}