using Common.Extensions;
using Infrastructure.Execution;
using Microsoft.Extensions.Logging.Abstractions;

namespace Benchmarks;

public class TaskQueueCollectTest {
    [GenerateSerializer]
    public class Payload { }

    public class Root : ClusterTestRoot<Payload> {
        public Root(ClusterTestUtils utils, BenchmarkStorage benchmarkStorage) : base(utils, benchmarkStorage) {
        }

        public override string Group => TestGroups.Infrastructure;
        public override string Title => "task-queue-collect";
        public override string MetricName => "ms";

        protected override Task Run(ClusterTestNodeHandle handle, Payload payload) {
            handle.Progress.SetStatus(OperationStatus.InProgress);

            var queue = new TaskQueue(NullLogger<TaskQueue>.Instance);

            var task1 = new TestPriorityTask("task-1", TaskPriority.Medium);
            var task2 = new TestPriorityTask("task-2", TaskPriority.High);

            queue.Enqueue(task1);
            queue.Enqueue(task2);

            var collected = queue.Collect();

            TestAssert.Equal(2, collected.Count, "collect count");
            TestAssert.True(
                collected.Any(t => t.Id == "task-1") && collected.Any(t => t.Id == "task-2"),
                "both tasks collected"
            );

            handle.Progress.SetProgress(0.5f);

            // Second collect should return empty — tasks already consumed
            var second = queue.Collect();
            TestAssert.Equal(0, second.Count, "second collect should be empty");

            handle.Progress.Log("Task queue collect test passed");
            handle.Progress.SetProgress(1f);

            return Task.CompletedTask;
        }
    }
}
