using Npgsql;
using NpgsqlTypes;

namespace Infrastructure.State;

// The SQL of the direct states: one command per (table, has extension) group, in the order of the records.
internal class DirectStateCommands
{
    public DirectStateCommands(IStateSerializer serializer)
    {
        _serializer = serializer;
    }

    private readonly IStateSerializer _serializer;

    public IReadOnlyList<NpgsqlBatchCommand> BuildWriteCommands(IReadOnlyDictionary<StateIdentity, IStateValue> records)
    {
        var groups =
            new Dictionary<(string TableName, bool HasExtension), List<(StateIdentity Identity, IStateValue Value)>>();

        foreach (var (identity, value) in records)
        {
            var key = (identity.TableName, identity.Extension != null);

            if (groups.TryGetValue(key, out var list) == false)
            {
                list = new List<(StateIdentity, IStateValue)>();
                groups[key] = list;
            }

            list.Add((identity, value));
        }

        var commands = new List<NpgsqlBatchCommand>(groups.Count);

        foreach (var ((tableName, hasExtension), entries) in groups)
        {
            var command = new NpgsqlBatchCommand();
            var values = new List<string>(entries.Count);

            for (var i = 0; i < entries.Count; i++)
            {
                var (identity, value) = entries[i];
                var json = _serializer.Serialize(value);

                command.Parameters.AddWithValue($"key{i}", identity.Key);
                command.Parameters.AddWithValue($"type{i}", identity.Type);
                command.Parameters.AddWithValue($"version{i}", value.Version);

                var p = command.Parameters.AddWithValue($"value{i}", json);
                p.NpgsqlDbType = NpgsqlDbType.Jsonb;

                if (hasExtension)
                {
                    command.Parameters.AddWithValue($"ext{i}", identity.Extension!);
                    values.Add($"(@key{i}, @type{i}, @version{i}, @value{i}::jsonb, @ext{i})");
                }
                else
                {
                    values.Add($"(@key{i}, @type{i}, @version{i}, @value{i}::jsonb)");
                }
            }

            var extensionCol = hasExtension ? ", extension" : "";
            var conflictCol = hasExtension ? ", extension" : "";

            command.CommandText = $@"
                INSERT INTO {tableName} (key, type, version, value{extensionCol})
                VALUES {string.Join(", ", values)}
                ON CONFLICT (key, type{conflictCol})
                DO UPDATE SET value = EXCLUDED.value, version = EXCLUDED.version
            ";

            commands.Add(command);
        }

        return commands;
    }

    public static IReadOnlyList<NpgsqlBatchCommand> BuildDeleteCommands(IReadOnlyList<StateIdentity> identities)
    {
        var groups = new Dictionary<(string TableName, bool HasExtension), List<StateIdentity>>();

        foreach (var identity in identities)
        {
            var key = (identity.TableName, identity.Extension != null);

            if (!groups.TryGetValue(key, out var list))
            {
                list = new List<StateIdentity>();
                groups[key] = list;
            }

            list.Add(identity);
        }

        var commands = new List<NpgsqlBatchCommand>(groups.Count);

        foreach (var ((tableName, hasExtension), entries) in groups)
        {
            var command = new NpgsqlBatchCommand();
            var conditions = new List<string>(entries.Count);

            for (var i = 0; i < entries.Count; i++)
            {
                command.Parameters.AddWithValue($"key{i}", entries[i].Key);
                command.Parameters.AddWithValue($"type{i}", entries[i].Type);

                if (hasExtension)
                {
                    command.Parameters.AddWithValue($"ext{i}", entries[i].Extension!);
                    conditions.Add($"(key = @key{i} AND type = @type{i} AND extension = @ext{i})");
                }
                else
                {
                    conditions.Add($"(key = @key{i} AND type = @type{i})");
                }
            }

            command.CommandText = $"DELETE FROM {tableName} WHERE {string.Join(" OR ", conditions)}";
            commands.Add(command);
        }

        return commands;
    }
}
