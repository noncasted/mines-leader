using Common;
using DeploySetup;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Tests.Fixtures;
using Xunit;

namespace Tests.Deploy;

/// <summary>
/// F2.10: direct-state tables carry only the primary key index; cleanup also empties the dead-letter table.
/// F2.39: drop also removes the dead-letter table.
/// Every test runs the migrator steps against its own empty database.
/// </summary>
public class StatesSchemaTests : IAsyncLifetime
{
    private readonly DatabaseFixture _database = new();
    private string _connectionString = string.Empty;

    public async ValueTask InitializeAsync()
    {
        await _database.InitializeAsync();

        var databaseName = $"migrator_{Guid.NewGuid():N}";

        await using (var connection = await _database.DataSource.OpenConnectionAsync())
        {
            await using var create = new NpgsqlCommand($"CREATE DATABASE \"{databaseName}\"", connection);
            await create.ExecuteNonQueryAsync();
        }

        _connectionString = new NpgsqlConnectionStringBuilder(_database.ConnectionString)
        {
            Database = databaseName
        }.ConnectionString;
    }

    public async ValueTask DisposeAsync()
    {
        NpgsqlConnection.ClearAllPools();
        await _database.DisposeAsync();
    }

    [Fact]
    public async Task Setup_NewTables_HaveOnlyPrimaryKeyIndex()
    {
        await StatesSetup.Run(CreateConfiguration());

        var tables = await ReadTables();
        tables.Should().NotBeEmpty();

        foreach (var table in tables)
        {
            var indexes = await ReadIndexes(table);
            indexes.Should().ContainSingle($"table {table} must have only its primary key index")
                   .Which.Should().Be($"{table}_pkey");
        }
    }

    [Fact]
    public async Task Setup_ExistingTableWithKeyIndex_DropsIt()
    {
        // A table of the migrator's own lookup, recreated with the schema of the old migrator.
        await StatesSetup.Run(CreateConfiguration());
        var table = (await ReadTables()).First();

        await Execute($"""
                       DROP TABLE {table};
                       CREATE TABLE {table} (
                           key character varying(512) not null,
                           type character varying(512) not null,
                           value jsonb NOT NULL,
                           version int NOT NULL,
                           primary key (key, type)
                       );
                       CREATE INDEX ix_{table} ON {table} USING btree (key, type);
                       INSERT INTO {table} (key, type, value, version) VALUES ('k', 't', '{"{}"}', 0);
                       """);

        await StatesSetup.Run(CreateConfiguration());
        await StatesSetup.Run(CreateConfiguration());

        (await ReadIndexes(table)).Should().Equal($"{table}_pkey");
        (await Count(table)).Should().Be(1, "the migration must keep the data");
    }

    [Fact]
    public async Task Cleanup_TruncatesDeadLetters()
    {
        var configuration = CreateConfiguration();

        await StatesSetup.Run(configuration);
        await SideEffectsSetup.Run(configuration);

        await Execute($"""
                       INSERT INTO {DbLookup.SE_DeadLetter} (id, payload, retry_count, created_at)
                       VALUES (gen_random_uuid(), '{"{}"}', 5, now());
                       """);

        await StatesCleanup.Run(configuration);

        (await Count(DbLookup.SE_DeadLetter)).Should().Be(0);
    }

    [Fact]
    public async Task Drop_RemovesEverySideEffectTable()
    {
        var configuration = CreateConfiguration();

        await StatesSetup.Run(configuration);
        await SideEffectsSetup.Run(configuration);

        (await ReadTables()).Should().Contain(DbLookup.SE_DeadLetter);

        await StatesDrop.Run(configuration);

        (await ReadTables()).Should().NotContain([
            DbLookup.SE_Queue, DbLookup.SE_Processing, DbLookup.SE_Retry, DbLookup.SE_DeadLetter
        ]);
    }

    // The test assembly has its own generated StatesLookup (with test states): read what the migrator created.
    private async Task<IReadOnlyList<string>> ReadTables()
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            "SELECT tablename FROM pg_tables WHERE schemaname = 'public' ORDER BY tablename", connection);

        var result = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
            result.Add(reader.GetString(0));

        return result;
    }

    private IConfigurationManager CreateConfiguration()
    {
        var configuration = new ConfigurationManager();
        configuration["postgres"] = _connectionString;
        return configuration;
    }

    private async Task<IReadOnlyList<string>> ReadIndexes(string table)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            "SELECT indexname FROM pg_indexes WHERE schemaname = 'public' AND tablename = @table ORDER BY indexname",
            connection);
        command.Parameters.AddWithValue("table", table);

        var result = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
            result.Add(reader.GetString(0));

        return result;
    }

    private async Task<long> Count(string table)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"SELECT count(*) FROM {table}", connection);
        return (long)(await command.ExecuteScalarAsync())!;
    }

    private async Task Execute(string sql)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
}
