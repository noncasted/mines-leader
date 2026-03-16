using System.Runtime.CompilerServices;
using Common;
using Common.Extensions;
using Infrastructure.State;
using Microsoft.Extensions.Logging;

namespace Infrastructure;

public class DbGrainReader<TState>
{
    public DbGrainReader(IOrleans orleans)
    {
        Orleans = orleans;
        _stateInfo = orleans.GrainStatesRegistry.States[typeof(TState).FullName!];
    }

    private readonly GrainStateInfo _stateInfo;

    public readonly DbGrainReaderSelect Select = new();
    public readonly DbGrainReaderWhere Where = new();

    public IOrleans Orleans { get; }

    public async Task<int> Count(CancellationToken cancellation = default)
    {
        await using var connection = await Orleans.DbSource.OpenConnection();
        await using var command = connection.CreateCommand();

        var query = $"SELECT COUNT(*) FROM {_stateInfo.TableName}";

        var where = Where.FormQuery();

        if (where != string.Empty)
            query += $" WHERE {where}";

        command.CommandText = query;
        Where.FillParameters(command);

        var result = await command.ExecuteScalarAsync(cancellation);

        if (result is long count)
            return (int)count;

        return 0;
    }

    public async IAsyncEnumerable<DbGrainEntry> Read([EnumeratorCancellation] CancellationToken cancellation = default)
    {
        await using var connection = await Orleans.DbSource.OpenConnection();
        await using var command = connection.CreateCommand();

        Select.Validate();
        var select = Select.FormQuery();
        var query = $"SELECT {select} FROM {_stateInfo.TableName}";

        var where = Where.FormQuery();

        if (where != string.Empty)
            query += $" WHERE {where}";

        command.CommandText = query;
        Where.FillParameters(command);

        await using var reader = await command.ExecuteReaderAsync(cancellation);

        while (await reader.ReadAsync(cancellation))
        {
            var entry = new DbGrainEntry();

            try
            {
                if (Select.Id == true)
                {
                    switch (_stateInfo.KeyType)
                    {

                        case GrainKeyType.Integer:
                        {
                            if (reader["key"] is not long key)
                                throw new InvalidOperationException("Grain ID fields are not present or invalid.");

                            entry.LongId = key;
                            break;
                        }
                        case GrainKeyType.String:
                        {
                            if (reader["key"] is not string key)
                                throw new InvalidOperationException("Grain ID fields are not present or invalid.");

                            entry.StringKey = key;
                            break;
                        }
                        case GrainKeyType.Guid:
                        {
                            if (reader["key"] is not Guid key)
                                throw new InvalidOperationException("Grain ID fields are not present or invalid.");

                            entry.GuidKey = key;
                            break;
                        }
                        case GrainKeyType.IntegerAndString:
                        {
                            if (reader["key"] is not long key)
                                throw new InvalidOperationException("Grain ID fields are not present or invalid.");

                            entry.LongId = key;
                            break;
                        }
                        case GrainKeyType.GuidAndString:
                        {
                            if (reader["key"] is not Guid key)
                                throw new InvalidOperationException("Grain ID fields are not present or invalid.");

                            entry.GuidKey = key;
                            break;
                        }
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }

                if (Select.Value == true)
                {
                    if (reader["payload"] is not byte[] payloadBytes)
                        throw new InvalidOperationException("Value binary field is not present or invalid.");

                    entry.Value = payloadBytes;
                }

                if (Select.Extension == true)
                {
                    if (reader["extension"] is not string extension)
                        throw new InvalidOperationException("Grain ID extension field is not present or invalid.");

                    entry.Extension = extension;
                }
            }
            catch (Exception e)
            {
                Orleans.Logger.LogError(e, "Error reading grain entry.");
            }

            yield return entry;
        }
    }
}

public class DbGrainEntry
{
    public string StringKey { get; set; }
    public long LongId { get; set; }
    public Guid GuidKey { get; set; }
    public string Extension { get; set; } = string.Empty;
    public byte[] Value { get; set; } = [];
}