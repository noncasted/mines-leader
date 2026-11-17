using Infrastructure.Execution;

namespace Framework.Tests.Execution;

// Holds its slot of the balancer from the start of its execution until the test releases it.
public class BlockingTask : IPriorityTask
{
    public BlockingTask(string id, TaskPriority priority)
    {
        Id = id;
        Priority = priority;
    }

    private readonly TaskCompletionSource _started = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _released = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public string Id { get; }
    public TaskPriority Priority { get; }
    public TimeSpan Delay => TimeSpan.Zero;
    public Task Started => _started.Task;

    public async Task Execute()
    {
        _started.TrySetResult();
        await _released.Task;
    }

    public void Release()
    {
        _released.TrySetResult();
    }
}
