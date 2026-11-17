using Npgsql;
using Xunit;

namespace Framework.Tests.Fixtures;

// The rows of one side effect in the tables of the test database, found by a text of its payload: the tests of a
// collection share the database, so a row is never taken by position.
public static class SideEffectRows
{
    // Waits for the dead letter row of the effect and returns its error message.
    public static async Task<string> WaitDeadLetterError(NpgsqlDataSource dataSource, string payloadMarker)
    {
        string? error = null;

        await TestWait.Until(async () => {
            error = await ReadDeadLetterError(dataSource, payloadMarker);
            return error != null;
        });

        return error!;
    }

    public static async Task<int> Count(NpgsqlDataSource dataSource, string table, string payloadMarker)
    {
        await using var connection = await dataSource.OpenConnectionAsync(TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*)::int FROM {table} WHERE payload::text LIKE @marker";
        command.Parameters.AddWithValue("marker", $"%{payloadMarker}%");
        return (int)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;
    }

    private static async Task<string?> ReadDeadLetterError(NpgsqlDataSource dataSource, string payloadMarker)
    {
        await using var connection = await dataSource.OpenConnectionAsync(TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT error_message FROM side_effects_dead_letter WHERE payload::text LIKE @marker";
        command.Parameters.AddWithValue("marker", $"%{payloadMarker}%");
        return await command.ExecuteScalarAsync(TestContext.Current.CancellationToken) as string;
    }
}
