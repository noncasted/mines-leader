using FluentAssertions;
using Infrastructure.Execution;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Framework.Tests.Execution;

// The queue and the scheduler do not swallow a failure: Enqueue, Collect and Schedule throw it to their caller, which
// reacts (the balancer logs a failed collect and pauses), instead of logging it and returning nothing.
public class TaskQueueFailureTests
{
    private readonly TaskQueue _queue = new(NullLogger<TaskQueue>.Instance);

    [Fact]
    public void Enqueue_TaskFails_ReachesTheCaller()
    {
        var enqueue = () => _queue.Enqueue(new BrokenTask(idReadsBeforeFailure: int.MaxValue, delayFails: true));

        enqueue.Should().Throw<InvalidOperationException>().WithMessage(BrokenTask.Failure);
    }

    [Fact]
    public void Collect_TaskFails_ReachesTheCaller()
    {
        // The first read of the id is the key of Enqueue, the second one is the removal in Collect.
        _queue.Enqueue(new BrokenTask(idReadsBeforeFailure: 1, delayFails: false));

        var collect = () => _queue.Collect();

        collect.Should().Throw<InvalidOperationException>().WithMessage(BrokenTask.Failure);
    }

    [Fact]
    public void Schedule_EnqueueFails_ReachesTheCaller()
    {
        var queue = Substitute.For<ITaskQueue>();
        var failure = new InvalidOperationException("queue failed");
        queue.When(q => q.Enqueue(Arg.Any<IPriorityTask>())).Do(_ => throw failure);

        var schedule = () => new Infrastructure.Execution.TaskScheduler(queue).Schedule(new FakeTask("t", TimeSpan.Zero));

        schedule.Should().Throw<InvalidOperationException>().Which.Should().BeSameAs(failure);
    }
}
