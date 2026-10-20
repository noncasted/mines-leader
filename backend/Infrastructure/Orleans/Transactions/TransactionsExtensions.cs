using Npgsql;

namespace Infrastructure;

public class TransactionBuilder
{
    public required TransactionParameters Parameters { get; init; }
    public required ITransactions Transactions { get; init; }
}

public static class TransactionsExtensions
{
    extension(ITransactions transactions)
    {
        public Task<TransactionResult> Run(Func<Task> action)
        {
            var parameters = new TransactionParameters
            {
                Action = action,
                Callbacks = []
            };

            return transactions.Process(parameters);
        }

        // Throws TransactionFailedException (original error inside) when the action throws or the commit fails:
        // the value of an action whose changes were not committed must not reach the caller.
        public async Task<T> Run<T>(Func<Task<T>> action)
        {
            T result = default!;

            var parameters = new TransactionParameters
            {
                Action = Process,
                Callbacks = []
            };

            var transactionResult = await transactions.Process(parameters);
            transactionResult.ThrowIfFailed();

            return result;

            async Task Process()
            {
                result = await action();
            }
        }

        // Same as Run(Func<Task>), but a failed transaction throws TransactionFailedException instead of
        // returning a result the caller may forget to check.
        public async Task RunOrThrow(Func<Task> action)
        {
            var result = await transactions.Run(action);
            result.ThrowIfFailed();
        }

        public TransactionBuilder CreateBuilder(Func<Task> action)
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

    extension(TransactionResult result)
    {
        public void ThrowIfFailed()
        {
            if (result.IsSuccess == true)
                return;

            throw new TransactionFailedException(result.Error
                ?? throw new InvalidOperationException("Failed transaction result has no error"));
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
            return builder.Transactions.Process(builder.Parameters);
        }
    }
}