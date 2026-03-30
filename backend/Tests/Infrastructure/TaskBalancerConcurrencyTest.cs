using Common.Extensions;
using Infrastructure.Execution;
using Microsoft.Extensions.Logging.Abstractions;

namespace Tests;

public class TaskBalancerConcurrencyTest {
    [GenerateSerializer]
    public class Payload { }

    public class Root : ClusterTestRoot<Payload> {
        public Root(ClusterTestUtils utils) : base(utils) {
        }

        public override string Group => TestGroups.Infrastructure;
        public override string Title => "task-balancer-concurrency";

        protected override async Task Run(ClusterTestNodeHandle handle, Payload payload) {
            handle.Progress.SetStatus(OperationStatus.InProgress);

            var maxConcurrent = 0;
            var currentConcurrent = 0;
            var completedCount = 0;
            var concurrentLock = new Lock();

            var queue = new TaskQueue(NullLogger<TaskQueue>.Instance);

            for (var i = 0; i < 6; i++) {
                queue.Enqueue(new TestPriorityTask($"slow-{i}", TaskPriority.Medium, execute: async () => {
                    lock (concurrentLock) {
                        currentConcurrent++;

                        if (currentConcurrent > maxConcurrent)
                            maxConcurrent = currentConcurrent;
                    }

                    await Task.Delay(200);

                    lock (concurrentLock) {
                        currentConcurrent--;
                        completedCount++;
                    }
                }));
            }

            var config = new TestBalancerConfig(new TaskBalancerOptions {
                EmptyDelayMs = 10,
                NextDelayMs = 10,
                ConcurrentTasks = 2
            });

            var balancer = new TaskBalancer(queue, NullLogger<TaskBalancer>.Instance, config);

            balancer.Run(handle.Lifetime);

            handle.Progress.SetProgress(0.3f);

            var elapsed = 0;

            while (completedCount < 6 && elapsed < 5000) {
                await Task.Delay(50);
                elapsed += 50;
            }

            TestAssert.Equal(6, completedCount, "all tasks completed");
            TestAssert.True(maxConcurrent <= 2, $"max concurrent was {maxConcurrent}, expected <= 2");
            TestAssert.True(maxConcurrent >= 2, $"max concurrent was {maxConcurrent}, expected >= 2");

            handle.Progress.Log("Task balancer concurrency test passed");
            handle.Progress.SetProgress(1f);
        }
    }
}
