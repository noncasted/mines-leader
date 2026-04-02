using System.Diagnostics.CodeAnalysis;
using Common.Extensions;
using Infrastructure;

namespace Benchmarks;

public class DurableQueueDeliveryTest {
    [GenerateSerializer]
    public class TestMessage {
        [Id(0)]
        public Guid Id { get; set; }

        [Id(1)]
        public string Value { get; set; } = string.Empty;
    }

    public class DurableQueueTestId : IDurableQueueId {
        public string ToRaw() => "test-durable-delivery";
    }

    [GenerateSerializer]
    [method: SetsRequiredMembers]
    public class StartPayload() {
        [Id(0)]
        public int MessageCount { get; set; } = 10000;
    }

    public class Root : BenchmarkRoot<StartPayload> {
        public Root(ClusterTestUtils utils) : base(utils) {
        }

        public override string Group => TestGroups.Messaging;
        public override string Title => "durable-queue-delivery";
        public override string MetricName => "msg/s";

        protected override async Task Run(BenchmarkNodeHandle handle, StartPayload payload) {
            handle.Progress.SetStatus(OperationStatus.InProgress);

            var queueId = new DurableQueueTestId();
            var totalMessages = payload.MessageCount;
            var receivedCount = 0;
            var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            await Messaging.ListenDurableQueue<TestMessage>(handle.Lifetime, queueId, message => {
                var count = Interlocked.Increment(ref receivedCount);
                handle.Metrics.Inc();
                handle.Progress.SetProgress((float)count / totalMessages);

                if (count >= totalMessages)
                    completion.TrySetResult();
            });

            handle.Progress.Log("Listener ready, sending messages...");

            for (var i = 0; i < totalMessages; i++) {
                await Messaging.PushDirectQueue(queueId, new TestMessage {
                    Id = Guid.NewGuid(),
                    Value = $"msg-{i}"
                });
            }

            handle.Progress.Log($"Sent {totalMessages} messages, waiting for delivery...");

            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

            try {
                await completion.Task.WaitAsync(cts.Token);
            }
            catch (OperationCanceledException) {
                throw new Exception(
                    $"Timeout: received {receivedCount}/{totalMessages} messages");
            }

            handle.Progress.Log($"All {totalMessages} messages delivered");
            handle.Progress.SetProgress(1f);
        }
    }
}
