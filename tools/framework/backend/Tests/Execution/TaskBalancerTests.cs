using Framework.Tests.Fixtures;
using Internal;
using FluentAssertions;
using Infrastructure.Execution;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Framework.Tests.Execution;

public class TaskBalancerTests : LifetimeTest
{
    private readonly TaskQueue _queue;
    private readonly TaskBalancer _balancer;

    public TaskBalancerTests()
    {
        var queueLogger = Substitute.For<ILogger<TaskQueue>>();
        var balancerLogger = Substitute.For<ILogger<TaskBalancer>>();
        _queue = new TaskQueue(queueLogger);

        var config = CreateConfig(new TaskBalancerOptions
        {
            EmptyDelayMs = 10,
            NextDelayMs = 10,
            IterationScore = 1,
            ExceptionPenalty = 50,
            ConcurrentTasks = 10
        });
        _balancer = new TaskBalancer(_queue, balancerLogger, config);
    }

    [Fact]
    public async Task CriticalPriority_ExecutesBeforeLow()
    {
        var executionOrder = new List<string>();

        var lowTask = new OrderTrackingTask("low", TaskPriority.Low, executionOrder);
        var criticalTask = new OrderTrackingTask("critical", TaskPriority.Critical, executionOrder);

        _queue.Enqueue(lowTask);
        _queue.Enqueue(criticalTask);

        var config = CreateConfig(new TaskBalancerOptions
        {
            EmptyDelayMs = 10,
            NextDelayMs = 10,
            IterationScore = 1,
            ExceptionPenalty = 50,
            ConcurrentTasks = 1
        });
        var logger = Substitute.For<ILogger<TaskBalancer>>();
        var balancer = new TaskBalancer(_queue, logger, config);

        var lifetime = TestLifetime.Child();
        await balancer.Run(lifetime);

        await TestWait.Until(() => executionOrder.Count >= 2);

        lifetime.Terminate();

        executionOrder[0].Should().Be("critical");
        executionOrder[1].Should().Be("low");
    }

    [Fact]
    public async Task FailedTask_ReEnqueuedToQueue()
    {
        var task = new FakeTask("failing", delay: TimeSpan.Zero, priority: TaskPriority.Medium)
            .SetShouldFail(true);

        _queue.Enqueue(task);

        var lifetime = TestLifetime.Child();
        await _balancer.Run(lifetime);

        await TestWait.Until(() => task.ExecuteCount >= 2);

        lifetime.Terminate();

        task.ExecuteCount.Should()
            .BeGreaterThanOrEqualTo(2,
                "failed task should be re-enqueued and retried");
    }

    // A task waiting since an earlier collect gains IterationScore on every collect that brings new tasks, so it beats a
    // fresh task of a higher priority. The only slot is held until both wait.
    [Fact]
    public async Task AgingMechanism_OlderTasksGetHigherScore()
    {
        var executionOrder = new List<string>();
        var queue = new CollectSignalQueue(_queue);
        var blocker = new BlockingTask("blocker", TaskPriority.Critical);

        var config = CreateConfig(new TaskBalancerOptions
        {
            EmptyDelayMs = 10,
            NextDelayMs = 10,
            IterationScore = 100,
            ExceptionPenalty = 50,
            ConcurrentTasks = 1
        });
        var balancer = new TaskBalancer(queue, Substitute.For<ILogger<TaskBalancer>>(), config);

        var lifetime = TestLifetime.Child();
        await balancer.Run(lifetime);

        queue.Enqueue(blocker);
        await blocker.Started.WaitAsync(TestContext.Current.CancellationToken);

        queue.Enqueue(new OrderTrackingTask("low-old", TaskPriority.Low, executionOrder));
        await queue.Collected("low-old").WaitAsync(TestContext.Current.CancellationToken);

        queue.Enqueue(new OrderTrackingTask("high-new", TaskPriority.High, executionOrder));
        await queue.Collected("high-new").WaitAsync(TestContext.Current.CancellationToken);

        blocker.Release();
        await TestWait.Until(() => executionOrder.LockedCount() >= 2);

        lifetime.Terminate();

        // Low: 10 + 100 of the collect that brought the high one; high: 30.
        executionOrder.Should().Equal(["low-old", "high-new"], "the aged low-priority task beats the fresh high-priority one");
    }

    [Fact]
    public async Task MultipleTasks_AllExecutedExactlyOnce()
    {
        var tasks = Enumerable.Range(0, 5)
                              .Select(i => new FakeTask($"t{i}", delay: TimeSpan.Zero, priority: TaskPriority.Medium))
                              .ToList();

        foreach (var task in tasks)
            _queue.Enqueue(task);

        var lifetime = TestLifetime.Child();
        await _balancer.Run(lifetime);

        await TestWait.Until(() => tasks.All(t => t.ExecuteCount >= 1));

        lifetime.Terminate();

        // Each task should execute exactly once — successful tasks are not re-enqueued
        tasks.Should().AllSatisfy(t => t.ExecuteCount.Should().Be(1));
    }

    [Fact]
    public async Task LifetimeTermination_StopsProcessing()
    {
        var logger = new RecordingLogger<TaskBalancer>();
        var balancer = new TaskBalancer(_queue, logger, CreateConfig(new TaskBalancerOptions
        {
            EmptyDelayMs = 10,
            NextDelayMs = 10,
            IterationScore = 1,
            ExceptionPenalty = 50,
            ConcurrentTasks = 10
        }));

        var lifetime = TestLifetime.Child();
        await balancer.Run(lifetime);
        lifetime.Terminate();

        // The execute loop logs its drain as its last step: after it nothing executes a task.
        await TestWait.Until(() => logger.Count(LogLevel.Information, "[TaskBalancer] Drained") == 1);

        var task = new FakeTask("late", delay: TimeSpan.Zero);
        _queue.Enqueue(task);

        task.ExecuteCount.Should().Be(0, "tasks enqueued after lifetime termination should not execute");
    }

    private static ITaskBalancerConfig CreateConfig(TaskBalancerOptions options)
    {
        var config = Substitute.For<ITaskBalancerConfig>();
        config.Value.Returns(options);
        return config;
    }
}
