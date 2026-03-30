using Common.Extensions;
using Common.Reactive;
using Infrastructure.Execution;
using Microsoft.Extensions.Logging.Abstractions;

namespace Tests;

public class TaskBalancerExceptionPenaltyTest {
    [GenerateSerializer]
    public class Payload { }

    public class Root : ClusterTestRoot<Payload> {
        public Root(ClusterTestUtils utils) : base(utils) {
        }

        public override string Group => TestGroups.Infrastructure;
        public override string Title => "task-balancer-exception-penalty";

        protected override async Task Run(ClusterTestNodeHandle handle, Payload payload) {
            handle.Progress.SetStatus(OperationStatus.InProgress);

            var executionLog = new List<string>();
            var queue = new TaskQueue(NullLogger<TaskQueue>.Instance);

            // Failing task with high priority — after exception it gets penalty and re-scheduled
            queue.Enqueue(new TestPriorityTask("failing", TaskPriority.High, log: executionLog, failCount: 1));
            queue.Enqueue(new TestPriorityTask("normal", TaskPriority.Medium, log: executionLog));

            var config = new TestBalancerConfig(new TaskBalancerOptions {
                EmptyDelayMs = 10,
                NextDelayMs = 10,
                ConcurrentTasks = 1,
                IterationScore = 0,
                ExceptionPenalty = 100
            });

            var balancer = new TaskBalancer(queue, NullLogger<TaskBalancer>.Instance, config);

            balancer.Run(handle.Lifetime);

            handle.Progress.SetProgress(0.3f);

            var elapsed = 0;

            while (executionLog.Count < 3 && elapsed < 5000) {
                await Task.Delay(50);
                elapsed += 50;
            }

            // Execution order: failing (throws) -> normal -> failing (succeeds on retry)
            TestAssert.True(executionLog.Count >= 3, $"expected at least 3 executions, got {executionLog.Count}");
            TestAssert.Equal("failing", executionLog[0], "failing task tried first (high priority)");
            TestAssert.Equal("normal", executionLog[1], "normal task runs while failing is penalized");
            TestAssert.Equal("failing", executionLog[2], "failing task retried after penalty");

            handle.Progress.Log("Task balancer exception penalty test passed");
            handle.Progress.SetProgress(1f);
        }
    }
}
