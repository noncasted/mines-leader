namespace Infrastructure;

public interface IOldTransactions
{
    ITransactionClient Client { get; }
    ITransactionRunner Runner { get; }
}

public class OldTransactions : IOldTransactions
{
    public OldTransactions(ITransactionClient client, ITransactionRunner runner)
    {
        Client = client;
        Runner = runner;
    }

    public ITransactionClient Client { get; }
    public ITransactionRunner Runner { get; }
}

public static class TransactionsExtensions
{
    extension(IOldTransactions oldTransactions)
    {
        public Task Create(Func<Task> action)
        {
            return oldTransactions.Client.RunTransaction(TransactionOption.Create, action);
        }

        public Task Join(Func<Task> action)
        {
            return oldTransactions.Client.RunTransaction(TransactionOption.Join, action);
        }

        public Task<T> Create<T>(Func<Task<T>> action)
        {
            return oldTransactions.Run(TransactionOption.Create, action);
        }

        public Task<T> Join<T>(Func<Task<T>> action)
        {
            return oldTransactions.Run(TransactionOption.Join, action);
        }

        public async Task<T> Run<T>(
            TransactionOption option,
            Func<Task<T>> action)
        {
            T result = default!;

            await oldTransactions.Client.RunTransaction(option, async () =>
                {
                    result = await action();
                }
            );

            return result;
        }
    }
}