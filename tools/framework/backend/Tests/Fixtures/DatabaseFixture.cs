using Common;
using Npgsql;
using Shared;

namespace Framework.Tests.Fixtures;

// A test database of its own in the container of the run (PostgresContainer).
// Creates the state tables of TestStates and the side effects tables.
public class DatabaseFixture : IAsyncDisposable
{
    private string _databaseName = string.Empty;
    private string _connectionString = string.Empty;

    public string ConnectionString => _connectionString;
    public NpgsqlDataSource DataSource { get; private set; } = null!;

    public async Task Initialize()
    {
        _databaseName = $"test_db_{Guid.NewGuid():N}";

        // Create the test database
        var masterConnectionString = await PostgresContainer.GetConnectionString();
        await using var masterConnection = new NpgsqlConnection(masterConnectionString);
        await masterConnection.OpenAsync();
        await using var createDbCmd = masterConnection.CreateCommand();
        createDbCmd.CommandText = $"CREATE DATABASE \"{_databaseName}\"";
        await createDbCmd.ExecuteNonQueryAsync();

        _connectionString = new NpgsqlConnectionStringBuilder(masterConnectionString)
        {
            Database = _databaseName
        }.ConnectionString;

        DataSource = NpgsqlDataSource.Create(_connectionString);

        await InitializeSchema();
    }

    private async Task InitializeSchema()
    {
        await using var connection = await DataSource.OpenConnectionAsync();

        // The state tables of the registry, the production schema (the StatesSetup of the projects).
        var createdTables = new HashSet<string>();

        foreach (var info in TestStates.All)
        {
            if (!createdTables.Add(info.TableName))
                continue;

            var (keyDef, indexDef) = info.KeyType switch
            {
                GrainKeyType.Integer => ("bigint not null", "(key, type)"),
                GrainKeyType.String => ("character varying(512) not null", "(key, type)"),
                GrainKeyType.Guid => ("uuid not null", "(key, type)"),
                GrainKeyType.IntegerAndString => ("bigint not null, extension character varying(512) not null",
                    "(key, type, extension)"),
                GrainKeyType.GuidAndString => ("uuid not null, extension character varying(512) not null",
                    "(key, type, extension)"),
                _ => throw new ArgumentOutOfRangeException()
            };

            await using var cmd = connection.CreateCommand();

            cmd.CommandText = $"""
                               CREATE TABLE {info.TableName} (
                                   key {keyDef},
                                   type character varying(512) not null,
                                   value jsonb NOT NULL,
                                   version int NOT NULL,
                                   primary key {indexDef}
                               );
                               """;
            await cmd.ExecuteNonQueryAsync();
        }

        // Create side effects tables
        await using var seCmd = connection.CreateCommand();

        seCmd.CommandText = """
                            CREATE TABLE IF NOT EXISTS side_effects_queue (
                                id uuid PRIMARY KEY,
                                payload jsonb NOT NULL,
                                retry_count integer NOT NULL DEFAULT 0,
                                created_at timestamptz NOT NULL DEFAULT now()
                            );

                            CREATE TABLE IF NOT EXISTS side_effects_processing (
                                id uuid PRIMARY KEY,
                                payload jsonb NOT NULL,
                                retry_count integer NOT NULL DEFAULT 0,
                                created_at timestamptz NOT NULL,
                                processing_started_at timestamptz NOT NULL DEFAULT now()
                            );

                            CREATE TABLE IF NOT EXISTS side_effects_retry_queue (
                                id uuid PRIMARY KEY,
                                payload jsonb NOT NULL,
                                retry_count integer NOT NULL DEFAULT 0,
                                created_at timestamptz NOT NULL,
                                retry_after timestamptz NOT NULL
                            );

                            CREATE TABLE IF NOT EXISTS side_effects_dead_letter (
                                id uuid PRIMARY KEY,
                                payload jsonb NOT NULL,
                                retry_count integer NOT NULL DEFAULT 0,
                                created_at timestamptz NOT NULL,
                                failed_at timestamptz NOT NULL DEFAULT now(),
                                error_message text
                            );
                            """;
        await seCmd.ExecuteNonQueryAsync();
    }

    public async Task ResetDatabase()
    {
        await using var connection = await DataSource.OpenConnectionAsync();
        await using var command = connection.CreateCommand();

        command.CommandText = """
                              DO $$
                              DECLARE
                                  tbl text;
                              BEGIN
                                  FOR tbl IN
                                      SELECT tablename FROM pg_tables
                                      WHERE schemaname = 'public'
                                  LOOP
                                      EXECUTE format('TRUNCATE TABLE %I RESTART IDENTITY CASCADE', tbl);
                                  END LOOP;
                              END
                              $$;
                              """;
        await command.ExecuteNonQueryAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await DataSource.DisposeAsync();
    }
}
