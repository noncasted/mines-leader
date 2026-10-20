using Common;
using Common.Extensions;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Shared;

namespace DeploySetup;

public class StatesSetup
{
    public static async Task Run(IConfigurationManager configuration)
    {
        await using var connection = await configuration.GetConnection();

        foreach (var info in DeployStates.Select(configuration))
        {
            if (info.IsEventState)
                continue; // Marten inline projections manage their own tables

            if (await connection.IsTableExists(info.TableName) == true)
            {
                await DropRedundantKeyIndex(connection, info.TableName);
                continue;
            }

            string key;
            string primaryKey;

            switch (info.KeyType)
            {
                case GrainKeyType.Integer:
                    key = "bigint not null";
                    primaryKey = "(key, type)";
                    break;
                case GrainKeyType.String:
                    key = "character varying(512) not null";
                    primaryKey = "(key, type)";
                    break;
                case GrainKeyType.Guid:
                    key = "uuid not null";
                    primaryKey = "(key, type)";
                    break;
                case GrainKeyType.IntegerAndString:
                    key = "bigint not null, extension character varying(512) not null";
                    primaryKey = "(key, type, extension)";
                    break;
                case GrainKeyType.GuidAndString:
                    key = "uuid not null, extension character varying(512) not null";
                    primaryKey = "(key, type, extension)";
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            // The primary key already is a unique btree index on these columns: no extra index.
            var createTableQuery = $@"
                    CREATE TABLE {info.TableName} (
                        key {key} ,
                        type character varying(512) not null,
                        value jsonb NOT NULL,
                        version int NOT NULL,
                        primary key {primaryKey}
                    );
                    ";

            await using var createTableCommand = new NpgsqlCommand(createTableQuery, connection);
            await createTableCommand.ExecuteNonQueryAsync();
        }
    }

    // Tables created before 2026-10 also have ix_<table>, a btree on the primary key columns: it doubled the
    // index maintenance of every write without serving any query the primary key does not.
    private static async Task DropRedundantKeyIndex(NpgsqlConnection connection, string tableName)
    {
        await using var command = new NpgsqlCommand($"DROP INDEX IF EXISTS ix_{tableName};", connection);
        await command.ExecuteNonQueryAsync();
    }
}
