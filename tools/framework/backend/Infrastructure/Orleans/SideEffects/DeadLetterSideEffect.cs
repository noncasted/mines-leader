namespace Infrastructure;

// Sentinel used when payload deserialization fails — immediately fails without executing, with the original error.
internal class DeadLetterSideEffect : ISideEffect
{
    public DeadLetterSideEffect(Exception error)
    {
        _error = error;
    }

    private readonly Exception _error;

    public Task Execute(IOrleans orleans) =>
        Task.FromException(new InvalidOperationException(
            $"[SideEffects] Dead letter: failed to deserialize payload: {_error.Message}", _error));
}
