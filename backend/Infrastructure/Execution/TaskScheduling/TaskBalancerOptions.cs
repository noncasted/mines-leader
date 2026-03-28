namespace Infrastructure.Execution;

public class TaskBalancerOptions
{
    public int EmptyDelayMs { get; set; } = 500;
    public int NextDelayMs { get; set; } = 100;
    public int IterationScore { get; set; } = 1;
    public int ExceptionPenalty { get; set; } = 50;
    public int ConcurrentTasks { get; set; } = 10;
}

public interface ITaskBalancerConfig : IAddressableState<TaskBalancerOptions> { }
