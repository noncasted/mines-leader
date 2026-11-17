namespace Infrastructure;

// Thrown by Transactions.Process when the commit call failed and the database could not tell whether the transaction
// committed: not a failure to retry, the change may be in the database.
public class TransactionOutcomeUnknownException : Exception
{
    public TransactionOutcomeUnknownException(string transactionXid, Exception error)
        : base($"Outcome of database transaction {transactionXid} is unknown: {error.Message}", error)
    {
    }
}
