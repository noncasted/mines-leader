using Common.Extensions;
using Infrastructure.State;
using Npgsql;

namespace Coordinator;

public class StatesSetup
{
    public StatesSetup(IDbSource dbSource, IGrainStatesRegistry statesRegistry)
    {
        _dbSource = dbSource;
        _statesRegistry = statesRegistry;
    }

    private readonly IDbSource _dbSource;
    private readonly IGrainStatesRegistry _statesRegistry;

    public async Task Run()
    {
        await using var connection = await _dbSource.Value.OpenConnectionAsync();

        foreach (var (_, info) in _statesRegistry.States)
        {
            if (await IsTableExists(info.TableName) == true)
                continue;

            string key;
            string index;

            switch (info.KeyType)
            {
                case GrainKeyType.Integer:
                    key = "bigint not null";
                    index = "(key, type)";
                    break;
                case GrainKeyType.String:
                    key = "character varying(512) not null";
                    index = "(key, type)";

                    break;
                case GrainKeyType.Guid:
                    key = "uuid not null";
                    index = "(key, type)";

                    break;
                case GrainKeyType.IntegerAndString:
                    key = "bigint not null, extension character varying(512) not null";
                    index = "(key, type, extension)";

                    break;
                case GrainKeyType.GuidAndString:
                    key = "uuid not null, extension character varying(512) not null";
                    index = "(key, type, extension)";
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            var createTableQuery = $@"
                    CREATE TABLE {info.TableName} (
                        key {key} ,
                        type character varying(512) not null,
                        value jsonb NOT NULL,
                        primary key {index}
                    );

                    CREATE INDEX ix_{info.TableName}
                        ON {info.TableName} USING btree
                        {index};
                    ";

            await using var createTableCommand = new NpgsqlCommand(createTableQuery, connection);
            await createTableCommand.ExecuteNonQueryAsync();
        }

        return;

        async Task<bool> IsTableExists(string tableName)
        {
            var checkTableQuery = $@"
                SELECT EXISTS (
                    SELECT 1
                    FROM information_schema.tables
                    WHERE table_schema = 'public' AND table_name = '{tableName}'
                );";

            await using var checkTableCommand = new NpgsqlCommand(checkTableQuery, connection);
            var result = await checkTableCommand.ExecuteScalarAsync();

            if (result is not bool tableExists)
                throw new Exception("Failed to check if table exists");

            return tableExists;
        }

    }
}