using System.Collections.Concurrent;
using Infrastructure.Execution;

namespace Framework.Tests.Execution;

// A task queue that tells when a collect of the balancer took a task: from then on the task waits in the balancer.
public class CollectSignalQueue : ITaskQueue
{
    public CollectSignalQueue(ITaskQueue queue)
    {
        _queue = queue;
    }

    private readonly ITaskQueue _queue;
    private readonly ConcurrentDictionary<string, TaskCompletionSource> _collected = new();

    public void Enqueue(IPriorityTask task)
    {
        _queue.Enqueue(task);
    }

    public IReadOnlyList<IPriorityTask> Collect()
    {
        var tasks = _queue.Collect();

        foreach (var task in tasks)
            Signal(task.Id).TrySetResult();

        return tasks;
    }

    public Task Collected(string taskId)
    {
        return Signal(taskId).Task;
    }

    private TaskCompletionSource Signal(string taskId)
    {
        return _collected.GetOrAdd(taskId, _ => new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously));
    }
}
