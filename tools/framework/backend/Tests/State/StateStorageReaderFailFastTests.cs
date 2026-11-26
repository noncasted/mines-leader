using Common;
using Common.Extensions;
using FluentAssertions;
using Framework.Tests.Fixtures;
using Framework.Tests.Grains;
using Infrastructure;
using Infrastructure.State;
using NSubstitute;
using Xunit;

namespace Framework.Tests.State;

// A page of direct states is either read whole or throws: no empty page for a failed database, no skipped row that
// TotalCount still counts.
[Collection(nameof(OrleansIntegrationCollection))]
public class StateStorageReaderFailFastTests
    (OrleansTestClusterFixture fixture) : IntegrationTestBase<OrleansTestClusterFixture>(fixture)
{
    [Fact]
    public async Task ReadPage_DatabaseFails_Throws()
    {
        var dbSource = Substitute.For<IDbSource>();
        dbSource.Value.Returns(_ => throw new TimeoutException("database down"));

        var read = () => CreateReader(dbSource).ReadPage<Guid, SimpleTestState>(0, 10);

        await read.Should().ThrowAsync<TimeoutException>();
    }

    [Fact]
    public async Task ReadPage_RowDoesNotDeserialize_Throws()
    {
        var key = Guid.NewGuid();
        await Execute("INSERT INTO state_simple_test (key, type, value, version) VALUES (@key, 'simple_test', '{\"Counter\":\"broken\"}'::jsonb, 0)", key);

        try
        {
            var read = () => CreateReader(GetSiloService<IDbSource>()).ReadPage<Guid, SimpleTestState>(0, 1000);

            await read.Should().ThrowAsync<Exception>("a row that does not deserialize is not skipped");
        }
        finally
        {
            await Execute("DELETE FROM state_simple_test WHERE key = @key", key);
        }
    }

    private async Task Execute(string sql, Guid key)
    {
        await using var connection = await Database.DataSource.OpenConnectionAsync(TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("key", key);
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    private StateStorageReader CreateReader(IDbSource dbSource)
    {
        return new StateStorageReader(
            GetSiloService<IGrainStatesRegistry>(),
            GetSiloService<IEventStorage>(),
            dbSource,
            GetSiloService<IStateSerializer>(),
            GetSiloService<IStateMigrations>());
    }
}
