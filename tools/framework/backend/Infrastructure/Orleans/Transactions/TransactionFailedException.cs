namespace Infrastructure;

// Thrown by the value-returning transaction helpers when the transaction did not commit.
// InnerException is the original failure: the action exception or the commit error.
public class TransactionFailedException : Exception
{
    public TransactionFailedException(Exception error)
        : base($"Transaction failed: {error.Message}", error)
    {
    }
}
