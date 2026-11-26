using Common;
using Shared;

namespace Infrastructure;

[GrainState(Table = "configs", State = "side_effects_config", Lookup = "SideEffectsConfig", Key = GrainKeyType.String)]
// A record: the console edits a config with `with` on its own copy.
public record SideEffectsOptions
{
    public int ScanDelay { get; init; } = 5; // ms between worker iterations when work was found
    public int EmptyScanDelay { get; init; } = 2000; // ms between worker iterations when queue is empty
    public int ConcurrentExecutions { get; init; } = 50; // max parallel effects
    public int MaxRetryCount { get; init; } = 5; // max attempts
    public float IncrementalRetryDelay { get; init; } = 30; // seconds * retryCount = delay
    public int StuckCheckIntervalSeconds { get; init; } = 60; // how often to touch the own entries and requeue the stuck ones; well below StuckThresholdMinutes
    public float StuckThresholdMinutes { get; init; } = 5; // entries in processing not touched by their worker for this long are stuck
}
