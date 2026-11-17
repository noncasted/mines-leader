using Common.Extensions;
using FluentAssertions;
using Framework.Tests.Fixtures;
using Infrastructure.Execution;
using Internal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Framework.Tests.Execution;

// The failure paths of the TaskBalancer: the penalty of a failing task and the drain at the end of the lifetime.
[Collection(nameof(NoAwaitCollection))]
public class TaskBalancerFailureTests : NoAwaitHandlerTest
{
    [Fact]
    public async Task FailingCriticalTask_PenaltyLetsLowTasksRun()
    {
        var queue = new TaskQueue(NullLogger<TaskQueue>.Instance);
        var balancer = CreateBalancer(queue, concurrentTasks: 1);

        var criticalRuns = 0;
        var lowDone = 0;

        queue.Enqueue(new LambdaTask("critical", TaskPriority.Critical, () => {
            Interlocked.Increment(ref criticalRuns);
            throw new InvalidOperationException("always fails");
        }));

        // A backlog of low tasks, 50 ms each: about 0.5 s of work, five collect ticks of the balancer.
        for (var i = 0; i < 10; i++)
        {
            queue.Enqueue(new LambdaTask($"low-{i}", TaskPriority.Low, () => {
                Thread.Sleep(50);
                Interlocked.Increment(ref lowDone);
            }));
        }

        var lifetime = TestLifetime.Child();
        await balancer.Run(lifetime);
        await TestWait.Until(() => Volatile.Read(ref lowDone) == 10);
        var runs = Volatile.Read(ref criticalRuns);
        lifetime.Terminate();

        runs.Should().BeLessThanOrEqualTo(2,
            "after a failure the penalty (50) puts the critical task (40) below the low ones (10)");
    }

    [Fact]
    public async Task LifetimeEnds_DrainCompletesWithoutError()
    {
        var queue = new TaskQueue(NullLogger<TaskQueue>.Instance);
        var logger = new DrainLogger();
        var balancer = CreateBalancer(queue, concurrentTasks: 4, logger);
        var started = 0;
        var finished = 0;

        for (var i = 0; i < 4; i++)
        {
            queue.Enqueue(new LambdaTask($"task-{i}", TaskPriority.Medium, () => {
                Interlocked.Increment(ref started);
                Thread.Sleep(100);
                Interlocked.Increment(ref finished);
            }));
        }

        var lifetime = TestLifetime.Child();
        await balancer.Run(lifetime);
        await TestWait.Until(() => Volatile.Read(ref started) >= 1);

        lifetime.Terminate();
        await logger.Drained.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);

        Volatile.Read(ref finished).Should().Be(Volatile.Read(ref started), "the drain waits for every running task");

        Reported.Should().BeEmpty("the drain of the execute loop must not fail");
    }

    // The lifetime ends in the pause after a failed iteration of the execute loop: the loop still drains.
    [Fact]
    public async Task LifetimeEndsInPauseAfterFailedIteration_DrainCompletes()
    {
        var queue = Substitute.For<ITaskQueue>();
        queue.Collect().Returns(Array.Empty<IPriorityTask>());

        // Run reads the config synchronously: the collect loop, the execute loop (its slots, its iteration). The
        // iteration fails, the pause after it outlasts the test.
        var options = new TaskBalancerOptions { EmptyDelayMs = 60_000, ConcurrentTasks = 1 };
        var config = Substitute.For<ITaskBalancerConfig>();
        var reads = 0;

        config.Value.Returns(_ => Interlocked.Increment(ref reads) == 3
            ? throw new InvalidOperationException("the iteration fails")
            : options);

        var logger = new DrainLogger();
        var balancer = new TaskBalancer(queue, logger, config);

        var lifetime = TestLifetime.Child();
        await balancer.Run(lifetime);
        Volatile.Read(ref reads).Should().BeGreaterThanOrEqualTo(4, "the failed iteration has started its pause");

        lifetime.Terminate();
        await logger.Drained.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
    }

    private static TaskBalancer CreateBalancer(ITaskQueue queue, int concurrentTasks)
    {
        return CreateBalancer(queue, concurrentTasks, NullLogger<TaskBalancer>.Instance);
    }

    private static TaskBalancer CreateBalancer(ITaskQueue queue, int concurrentTasks, ILogger<TaskBalancer> logger)
    {
        var config = Substitute.For<ITaskBalancerConfig>();

        config.Value.Returns(new TaskBalancerOptions
        {
            EmptyDelayMs = 10,
            NextDelayMs = 100,
            IterationScore = 1,
            ExceptionPenalty = 50,
            ConcurrentTasks = concurrentTasks
        });

        return new TaskBalancer(queue, logger, config);
    }

    private class LambdaTask(string id, TaskPriority priority, Action action) : IPriorityTask
    {
        public string Id => id;
        public TaskPriority Priority => priority;
        public TimeSpan Delay => TimeSpan.Zero;

        public Task Execute()
        {
            action();
            return Task.CompletedTask;
        }
    }

    // The end of the drain of the execute loop is seen only in its log.
    private class DrainLogger : ILogger<TaskBalancer>
    {
        private readonly TaskCompletionSource _drained = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task Drained => _drained.Task;

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (formatter(state, exception) == "[TaskBalancer] Drained")
                _drained.TrySetResult();
        }
    }
}
