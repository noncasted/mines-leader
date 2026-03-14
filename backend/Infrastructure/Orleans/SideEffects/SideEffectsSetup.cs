using Common.Extensions;
using Npgsql;

namespace Infrastructure;

public class SideEffectsSetup
{
    public SideEffectsSetup(IDbSource dbSource)
    {
        _dbSource = dbSource;
    }

    private readonly IDbSource _dbSource;

    public async Task Run()
    {
        await using var connection = await _dbSource.Value.OpenConnectionAsync();

        await CreateIfNotExists(connection, "side_effects_queue", @"
            CREATE TABLE side_effects_queue (
                id uuid NOT NULL,
                payload jsonb NOT NULL,
                retry_count integer NOT NULL DEFAULT 0,
                created_at timestamptz NOT NULL,
                PRIMARY KEY (id)
            );
            CREATE INDEX ix_side_effects_queue ON side_effects_queue USING btree (created_at);
        ");

        await CreateIfNotExists(connection, "side_effects_processing", @"
            CREATE TABLE side_effects_processing (
                id uuid NOT NULL,
                payload jsonb NOT NULL,
                retry_count integer NOT NULL DEFAULT 0,
                created_at timestamptz NOT NULL,
                processing_started_at timestamptz NOT NULL,
                PRIMARY KEY (id)
            );
        ");

        await CreateIfNotExists(connection, "side_effects_retry_queue", @"
            CREATE TABLE side_effects_retry_queue (
                id uuid NOT NULL,
                payload jsonb NOT NULL,
                retry_count integer NOT NULL DEFAULT 0,
                created_at timestamptz NOT NULL,
                retry_after timestamptz NOT NULL,
                PRIMARY KEY (id)
            );
            CREATE INDEX ix_side_effects_retry_queue ON side_effects_retry_queue USING btree (retry_after);
        ");
    }

    private static async Task CreateIfNotExists(NpgsqlConnection connection, string tableName, string createSql)
    {
        var checkQuery = $@"
            SELECT EXISTS (
                SELECT 1 FROM information_schema.tables
                WHERE table_schema = 'public' AND table_name = '{tableName}'
            );";

        await using var checkCommand = new NpgsqlCommand(checkQuery, connection);
        var exists = (bool)(await checkCommand.ExecuteScalarAsync())!;

        if (exists)
            return;

        await using var createCommand = new NpgsqlCommand(createSql, connection);
        await createCommand.ExecuteNonQueryAsync();
    }
}
