namespace Infrastructure;

public class TransactionBuilder
{
    public required TransactionParameters Parameters { get; init; }
    public required ITransactions Transactions { get; init; }
}
