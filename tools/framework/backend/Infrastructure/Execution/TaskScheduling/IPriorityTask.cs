namespace Infrastructure.Execution;

public interface IPriorityTask
{
    string Id { get; }
    TaskPriority Priority { get; }
    TimeSpan Delay { get; }

    Task Execute();
}
