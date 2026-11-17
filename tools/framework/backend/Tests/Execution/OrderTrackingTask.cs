using Framework.Tests.Fixtures;
using Internal;
using FluentAssertions;
using Infrastructure.Execution;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Framework.Tests.Execution;

public class OrderTrackingTask : IPriorityTask
{
    public OrderTrackingTask(string id, TaskPriority priority, List<string> executionOrder)
    {
        Id = id;
        Priority = priority;
        _executionOrder = executionOrder;
    }

    private readonly List<string> _executionOrder;

    public string Id { get; }
    public TaskPriority Priority { get; }
    public TimeSpan Delay => TimeSpan.Zero;

    public Task Execute()
    {
        lock (_executionOrder)
        {
            _executionOrder.Add(Id);
        }

        return Task.CompletedTask;
    }
}
