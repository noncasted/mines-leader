using Common.Extensions;
using Infrastructure.Execution;
using Microsoft.Extensions.Logging.Abstractions;

namespace Tests;

public class TaskQueueDelayTest {
    [GenerateSerializer]
    public class Payload { }

    public class Root : ClusterTestRoot<Payload> {
        public Root(ClusterTestUtils utils) : base(utils) {
        }

        public override string Group => TestGroups.Infrastructure;
        public override string Title => "task-queue-delay";

        protected override Task Run(ClusterTestNodeHandle handle, Payload payload) {
            handle.Progress.SetStatus(OperationStatus.InProgress);

            var queue = new TaskQueue(NullLogger<TaskQueue>.Instance);

            var immediate = new TestPriorityTask("immediate", TaskPriority.Medium);
            var delayed = new TestPriorityTask("delayed", TaskPriority.Medium, TimeSpan.FromSeconds(60));

            queue.Enqueue(immediate);
            queue.Enqueue(delayed);

            var collected = queue.Collect();

            TestAssert.Equal(1, collected.Count, "only immediate task collected");
            TestAssert.Equal("immediate", collected[0].Id, "immediate task id");

            handle.Progress.SetProgress(0.5f);

            // Delayed task is still in the queue, not yet ready
            var second = queue.Collect();
            TestAssert.Equal(0, second.Count, "delayed task still waiting");

            handle.Progress.Log("Task queue delay test passed");
            handle.Progress.SetProgress(1f);

            return Task.CompletedTask;
        }
    }
}
