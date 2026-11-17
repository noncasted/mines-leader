using System.Diagnostics;
using System.Text;
using Common.Extensions;
using Internal;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Execution;

public interface ITaskBalancer
{
    Task Run(IReadOnlyLifetime lifetime);
}

public class TaskBalancer : ITaskBalancer
{
    public TaskBalancer(ITaskQueue queue, ILogger<TaskBalancer> logger, ITaskBalancerConfig config)
    {
        _queue = queue;
        _logger = logger;
        _config = config;
    }

    private readonly ILogger<TaskBalancer> _logger;
    private readonly ITaskBalancerConfig _config;
    private readonly ITaskQueue _queue;

    private readonly IReadOnlyDictionary<TaskPriority, int> _priorityToScore = new Dictionary<TaskPriority, int>
    {
        [TaskPriority.Low] = 10,
        [TaskPriority.Medium] = 20,
        [TaskPriority.High] = 30,
        [TaskPriority.Critical] = 40
    };

    private readonly Dictionary<string, TaskEntry> _scheduled = new();
    private readonly Lock _scheduledLock = new();

    // taskId → the failure penalties of a task, under _scheduledLock. A failed task comes back through the queue as
    // a new entry: the penalty of the removed entry was lost and a failing task ran again on every tick at full score.
    private readonly Dictionary<string, int> _penalties = new();

    public Task Run(IReadOnlyLifetime lifetime)
    {
        CollectLoop(lifetime).NoAwait();
        ExecuteLoop(lifetime).NoAwait();
        return Task.CompletedTask;
    }

    private async Task CollectLoop(IReadOnlyLifetime lifetime)
    {
        var failed = false;

        while (lifetime.IsTerminated == false)
        {
            try
            {
                // The pause after a failure stands inside the try: the end of the lifetime in it ends the loop here.
                if (failed)
                {
                    failed = false;
                    await Task.Delay(_config.Value.EmptyDelayMs, lifetime.Token);
                }

                var options = _config.Value;
                var items = _queue.Collect();

                if (items.Count == 0)
                {
                    await Task.Delay(options.EmptyDelayMs, lifetime.Token);
                    continue;
                }

                lock (_scheduledLock)
                {
                    var incoming = new HashSet<string>(items.Count);

                    foreach (var task in items)
                        incoming.Add(task.Id);

                    foreach (var (_, entry) in _scheduled)
                    {
                        if (incoming.Contains(entry.Key) == false)
                            entry.AddScore(options.IterationScore);
                    }

                    foreach (var task in items)
                    {
                        if (_scheduled.TryGetValue(task.Id, out var entry))
                        {
                            entry.AddScore(options.IterationScore);
                            continue;
                        }

                        var newEntry = new TaskEntry
                        {
                            Task = task,
                            Key = task.Id,
                            InitialScore = _priorityToScore[task.Priority] - _penalties.GetValueOrDefault(task.Id)
                        };
                        newEntry.Init();
                        _scheduled[task.Id] = newEntry;
                    }
                }

                LogEntries();

                await Task.Delay(options.NextDelayMs, lifetime.Token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "[TaskBalancer] CollectLoop iteration failed");
                failed = true;
            }
        }
    }

    private async Task ExecuteLoop(IReadOnlyLifetime lifetime)
    {
        // ConcurrentTasks is captured once — changing it requires a restart
        var concurrentTasks = _config.Value.ConcurrentTasks;
        var executionLock = new SemaphoreSlim(concurrentTasks, concurrentTasks);

        var failed = false;

        while (lifetime.IsTerminated == false)
        {
            var acquired = false;

            try
            {
                // The pause after a failure stands inside the try: the end of the lifetime in it still drains.
                if (failed)
                {
                    failed = false;
                    await Task.Delay(_config.Value.EmptyDelayMs, lifetime.Token);
                }

                var options = _config.Value;

                await executionLock.WaitAsync(lifetime.Token);
                acquired = true;

                if (TryPickMaxScored(out var entry) == false)
                {
                    executionLock.Release();
                    acquired = false;
                    TaskBalancerTelemetry.QueueDepth.Record(0);
                    await Task.Delay(options.EmptyDelayMs, lifetime.Token);
                    continue;
                }

                lock (_scheduledLock)
                {
                    TaskBalancerTelemetry.QueueDepth.Record(_scheduled.Count);
                }

                acquired = false;
                Execute(entry!).NoAwait();
            }
            catch (OperationCanceledException)
            {
                if (acquired)
                    executionLock.Release();
                break;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "[TaskBalancer] ExecuteLoop iteration failed");

                if (acquired)
                    executionLock.Release();

                failed = true;
            }
        }

        // Graceful drain: every permit back means every executing task has completed.
        _logger.LogInformation("[TaskBalancer] Draining {Count} executing tasks...", concurrentTasks - executionLock.CurrentCount);

        for (var i = 0; i < concurrentTasks; i++)
            await executionLock.WaitAsync();

        _logger.LogInformation("[TaskBalancer] Drained");

        return;

        async Task Execute(TaskEntry entry)
        {
            using var activity = TaskBalancerTelemetry.Source.StartActivity("TaskBalancer.Execute");
            activity?.SetTag("task.priority", entry.Task.Priority.ToString());
            activity?.SetTag("task.score", entry.Score);

            using var watch = MetricWatch.Start(TaskBalancerTelemetry.Duration);
            var stopwatch = Stopwatch.StartNew();
            var success = false;

            try
            {
                _logger.LogTrace("[TaskBalancer] Executing task, free handles: {Count}, taskId: {TaskId}",
                    executionLock.CurrentCount,
                    entry.Task.Id);

                await entry.Task.Execute();
                success = true;

                lock (_scheduledLock)
                    _penalties.Remove(entry.Key);
            }
            catch (Exception e)
            {
                stopwatch.Stop();
                activity?.SetStatus(ActivityStatusCode.Error, e.Message);

                lock (_scheduledLock)
                    _penalties[entry.Key] = _penalties.GetValueOrDefault(entry.Key) + _config.Value.ExceptionPenalty;

                _queue.Enqueue(entry.Task);

                _logger.LogError(e,
                    "[TaskBalancer] Task execution failed in {Time}, taskId: {TaskId}",
                    stopwatch.Elapsed,
                    entry.Task.Id);
            }
            finally
            {
                executionLock.Release();
                TaskBalancerTelemetry.Executed.Add(1);

                if (success)
                    TaskBalancerTelemetry.Success.Add(1);
                else
                    TaskBalancerTelemetry.Failure.Add(1);
            }

            if (success)
            {
                stopwatch.Stop();

                _logger.LogTrace("[TaskBalancer] Task execution completed in {Time}, taskId: {TaskId}",
                    stopwatch.Elapsed,
                    entry.Task.Id);
            }
        }
    }

    private void LogEntries()
    {
        lock (_scheduledLock)
        {
            if (_scheduled.Count == 0)
                return;

            _logger.LogDebug("[TaskBalancer] {Count} scheduled entries", _scheduled.Count);
        }
    }
    private bool TryPickMaxScored(out TaskEntry? maxEntry)
    {
        maxEntry = null;

        lock (_scheduledLock)
        {
            foreach (var (_, entry) in _scheduled)
                if (maxEntry == null || entry.Score > maxEntry.Score)
                    maxEntry = entry;

            if (maxEntry == null)
                return false;

            _scheduled.Remove(maxEntry.Key);
        }

        return true;
    }

    private class TaskEntry
    {
        public required string Key { get; init; }
        public required IPriorityTask Task { get; init; }
        public required int InitialScore { get; init; }

        private int _score;
        public int Score => _score;

        public void AddScore(int value) => Interlocked.Add(ref _score, value);

        public TaskEntry() => _score = 0;

        public void Init() => _score = InitialScore;
    }
}
