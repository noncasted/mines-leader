namespace Infrastructure;

public class TransactionResult
{
    public required bool IsSuccess { get; init; }
    public Exception? Error { get; init; }

    public override string ToString() => Error != null
        ? $"TransactionResult(Success={IsSuccess}, Error={Error})"
        : $"TransactionResult(Success={IsSuccess})";
}
