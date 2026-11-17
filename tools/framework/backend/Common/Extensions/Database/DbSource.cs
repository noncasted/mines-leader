using Microsoft.Extensions.Configuration;
using Npgsql;

namespace Common.Extensions;

public interface IDbSource
{
    NpgsqlDataSource Value { get; }
}

// The container that built it disposes it with the host: the pool of the data source closes its connections.
public class DbSource : IDbSource, IAsyncDisposable
{
    public DbSource(IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConnectionNames.Postgres) ??
                               throw new InvalidOperationException(
                                   $"No connection string {ConnectionNames.Postgres} in the configuration");

        Value = NpgsqlDataSource.Create(connectionString);
    }

    public NpgsqlDataSource Value { get; }

    public ValueTask DisposeAsync()
    {
        return Value.DisposeAsync();
    }
}

public static class DbSourceExtensions
{
    public static ValueTask<NpgsqlConnection> OpenConnection(this IDbSource dbSource)
    {
        return dbSource.Value.OpenConnectionAsync(CancellationToken.None);
    }
}