namespace Common;

// The tables of the side effects (Infrastructure SideEffectsStorage); the deploy setup of a game creates and drops them.
public static class SideEffectTables
{
    public const string Queue = "side_effects_queue";
    public const string Processing = "side_effects_processing";
    public const string RetryQueue = "side_effects_retry_queue";
    public const string DeadLetter = "side_effects_dead_letter";
}
