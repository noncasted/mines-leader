using FluentAssertions;
using Infrastructure.Execution;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Framework.Tests.Execution;

public class FakeTask : IPriorityTask
{
    public FakeTask(string id, TimeSpan delay, TaskPriority priority = TaskPriority.Medium)
    {
        Id = id;
        Delay = delay;
        Priority = priority;
    }

    public string Id { get; }
    public TaskPriority Priority { get; }
    public TimeSpan Delay { get; }

    private int _executeCount;
    public int ExecuteCount => _executeCount;

    private bool _shouldFail;

    public FakeTask SetShouldFail(bool shouldFail)
    {
        _shouldFail = shouldFail;
        return this;
    }

    public Task Execute()
    {
        Interlocked.Increment(ref _executeCount);

        if (_shouldFail)
            throw new InvalidOperationException("Task failed");

        return Task.CompletedTask;
    }
}
