using Npgsql;

namespace Infrastructure.State;

public static class TransactionsExtensions
{
    public class TransactionBuilder
    {
        public required TransactionParameters Parameters { get; init; }
        public required ITransactions Transactions { get; init; }
    }

    extension(ITransactions transactions)
    {
        public Task<TransactionResult> Run(Func<Task> action)
        {
            var parameters = new TransactionParameters
            {
                Action = action,
                Callbacks = []
            };

            return transactions.Run(parameters);
        }

        public TransactionBuilder Create(Func<Task> action)
        {
            var builder = new TransactionBuilder
            {
                Parameters = new TransactionParameters
                {
                    Action = action,
                    Callbacks = new List<Func<NpgsqlTransaction, Task>>()
                },
                Transactions = transactions
            };

            return builder;
        }
    }

    extension(TransactionBuilder builder)
    {
        public TransactionBuilder WithCallback(Func<NpgsqlTransaction, Task> callback)
        {
            builder.Parameters.Callbacks.Add(callback);
            return builder;
        }

        public Task<TransactionResult> Run()
        {
            return builder.Transactions.Run(builder.Parameters);
        }
    }
}