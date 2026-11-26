using Common.Extensions;

namespace Infrastructure;

// Asks the server what became of a database transaction whose commit call failed.
internal class TransactionCommitStatus
{
    public TransactionCommitStatus(IDbSource dbSource)
    {
        _dbSource = dbSource;
    }

    private readonly IDbSource _dbSource;

    public async Task<bool> IsCommitted(string transactionXid, Exception commitError)
    {
        const int attempts = 50;

        try
        {
            await using var connection = await _dbSource.Value.OpenConnectionAsync();

            for (var attempt = 0; attempt < attempts; attempt++)
            {
                await using var command = connection.CreateCommand();
                command.CommandText = "SELECT pg_xact_status(@xid::xid8)";
                command.Parameters.AddWithValue("xid", transactionXid);

                switch (await command.ExecuteScalarAsync())
                {
                    case "committed":
                        return true;
                    case "aborted":
                        return false;
                    case "in progress":
                        // The server still finishes the transaction of the broken connection.
                        await Task.Delay(100);
                        continue;
                    default:
                        throw new TransactionOutcomeUnknownException(transactionXid, commitError);
                }
            }
        }
        catch (Exception e) when (e is not TransactionOutcomeUnknownException)
        {
            throw new TransactionOutcomeUnknownException(transactionXid, new AggregateException(commitError, e));
        }

        throw new TransactionOutcomeUnknownException(transactionXid, commitError);
    }
}
