using Npgsql;

namespace Infrastructure;

public class TransactionParameters
{
    public required Func<Task> Action { get; init; }
    public required List<Func<NpgsqlTransaction, Task>> Callbacks { get; init; }
}
