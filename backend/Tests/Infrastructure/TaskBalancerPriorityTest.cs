using Common.Extensions;
using Common.Reactive;
using Infrastructure.Execution;
using Microsoft.Extensions.Logging.Abstractions;

namespace Tests;

public class TaskBalancerPriorityTest {
    [GenerateSerializer]
    public class Payload { }

    public class Root : ClusterTestRoot<Payload> {
        public Root(ClusterTestUtils utils) : base(utils) {
        }

        public override string Group => TestGroups.Infrastructure;
        public override string Title => "task-balancer-priority";

        protected override async Task Run(ClusterTestNodeHandle handle, Payload payload) {
            handle.Progress.SetStatus(OperationStatus.InProgress);

            var executionLog = new List<string>();
            var queue = new TaskQueue(NullLogger<TaskQueue>.Instance);

            queue.Enqueue(new TestPriorityTask("low", TaskPriority.Low, log: executionLog));
            queue.Enqueue(new TestPriorityTask("critical", TaskPriority.Critical, log: executionLog));
            queue.Enqueue(new TestPriorityTask("medium", TaskPriority.Medium, log: executionLog));

            var config = new TestBalancerConfig(new TaskBalancerOptions {
                EmptyDelayMs = 10,
                NextDelayMs = 10,
                ConcurrentTasks = 1, // sequential to guarantee order
                IterationScore = 0   // disable score accumulation so initial priority wins
            });

            var balancer = new TaskBalancer(queue, NullLogger<TaskBalancer>.Instance, config);

            balancer.Run(handle.Lifetime);

            handle.Progress.SetProgress(0.3f);

            var elapsed = 0;

            while (executionLog.Count < 3 && elapsed < 5000) {
                await Task.Delay(50);
                elapsed += 50;
            }

            TestAssert.Equal(3, executionLog.Count, "all tasks executed");
            TestAssert.Equal("critical", executionLog[0], "critical first");
            TestAssert.Equal("medium", executionLog[1], "medium second");
            TestAssert.Equal("low", executionLog[2], "low last");

            handle.Progress.Log("Task balancer priority test passed");
            handle.Progress.SetProgress(1f);
        }
    }
}
