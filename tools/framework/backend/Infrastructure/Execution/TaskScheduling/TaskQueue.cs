using System.Text;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Execution;

public interface ITaskQueue
{
    void Enqueue(IPriorityTask task);
    IReadOnlyList<IPriorityTask> Collect();
}

public class TaskQueue : ITaskQueue
{
    public TaskQueue(ILogger<TaskQueue> logger)
    {
        _logger = logger;
    }

    private readonly Lock _lock = new();
    private readonly ILogger<TaskQueue> _logger;
    private readonly Dictionary<string, Entry> _queue = new();

    public void Enqueue(IPriorityTask task)
    {
        lock (_lock)
        {
            _queue.TryAdd(task.Id, new Entry
            {
                Task = task,
                ScheduleDate = DateTime.UtcNow + task.Delay
            });
        }
    }

    public IReadOnlyList<IPriorityTask> Collect()
    {
        lock (_lock)
        {
            var tasks = new List<IPriorityTask>(_queue.Count);
            var trace = _logger.IsEnabled(LogLevel.Trace) ? new StringBuilder() : null;
            trace?.AppendLine($"[TaskQueue] Collecting tasks ({_queue.Count}): ");

            var now = DateTime.UtcNow;

            foreach (var (_, entry) in _queue)
            {
                if (now < entry.ScheduleDate)
                {
                    trace?.AppendLine(
                        $"    Skipping task {entry.Task.Id}, wait for {(entry.ScheduleDate - now).TotalSeconds:F1}s");

                    continue;
                }

                tasks.Add(entry.Task);
                trace?.AppendLine($"    Scheduling task {entry.Task.Id}");
            }

            foreach (var task in tasks)
                _queue.Remove(task.Id);

            if (trace != null)
                _logger.LogTrace(trace.ToString());

            return tasks;
        }
    }

    private class Entry
    {
        public required IPriorityTask Task { get; init; }
        public required DateTime ScheduleDate { get; init; }
    }
}
