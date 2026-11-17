using Testcontainers.PostgreSql;
using Xunit;

[assembly: AssemblyFixture(typeof(Framework.Tests.Fixtures.PostgresContainer))]

namespace Framework.Tests.Fixtures;

// The PostgreSQL container of the test process, one for the whole run: every DatabaseFixture creates its database
// in it. It starts when the first fixture asks for it, so a run without integration tests needs no Docker, and xUnit
// disposes it as an assembly fixture when the run ends.
public sealed class PostgresContainer : IAsyncDisposable
{
    private static readonly Lazy<Task<PostgreSqlContainer>> _container = new(Start);

    public static async Task<string> GetConnectionString()
    {
        var container = await _container.Value;
        return container.GetConnectionString();
    }

    private static async Task<PostgreSqlContainer> Start()
    {
        var container = new PostgreSqlBuilder("postgres:17")
                        .WithUsername("test")
                        .WithPassword("test")
                        .Build();

        await container.StartAsync();
        return container;
    }

    public async ValueTask DisposeAsync()
    {
        if (_container.IsValueCreated == false)
            return;

        var container = await _container.Value;
        await container.DisposeAsync();
    }
}
