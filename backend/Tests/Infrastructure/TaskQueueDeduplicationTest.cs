using Common.Extensions;
using Infrastructure.Execution;
using Microsoft.Extensions.Logging.Abstractions;

namespace Tests;

public class TaskQueueDeduplicationTest {
    [GenerateSerializer]
    public class Payload { }

    public class Root : ClusterTestRoot<Payload> {
        public Root(ClusterTestUtils utils) : base(utils) {
        }

        public override string Group => TestGroups.Infrastructure;
        public override string Title => "task-queue-deduplication";

        protected override Task Run(ClusterTestNodeHandle handle, Payload payload) {
            handle.Progress.SetStatus(OperationStatus.InProgress);

            var queue = new TaskQueue(NullLogger<TaskQueue>.Instance);

            var task1 = new TestPriorityTask("same-id", TaskPriority.Low);
            var task2 = new TestPriorityTask("same-id", TaskPriority.High);

            queue.Enqueue(task1);
            queue.Enqueue(task2);

            var collected = queue.Collect();

            TestAssert.Equal(1, collected.Count, "deduplication: should keep only first");
            TestAssert.Equal(TaskPriority.Low, collected[0].Priority, "deduplication: first enqueued wins");

            handle.Progress.Log("Task queue deduplication test passed");
            handle.Progress.SetProgress(1f);

            return Task.CompletedTask;
        }
    }
}
