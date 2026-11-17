using Common;
using Shared;

namespace Infrastructure.Execution;

[GrainState(Table = "configs", State = "task_balancer_config", Lookup = "TaskBalancerConfig",
    Key = GrainKeyType.String)]
// A record: the console edits a config with `with` on its own copy.
public record TaskBalancerOptions
{
    public int EmptyDelayMs { get; init; } = 500;
    public int NextDelayMs { get; init; } = 100;
    public int IterationScore { get; init; } = 1;
    public int ExceptionPenalty { get; init; } = 50;
    public int ConcurrentTasks { get; init; } = 10;
}
