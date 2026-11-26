namespace Infrastructure;

public static class TransactionContextProvider
{
    private static readonly AsyncLocal<TransactionContext?> _current = new();

    public static TransactionContext? Current => _current.Value;

    public static void SetCurrent(TransactionContext transactionContext)
    {
        _current.Value = transactionContext;
    }

    public static void Clear()
    {
        _current.Value = null;
    }
}
