using Common.Extensions;
using Infrastructure;

namespace Tests;

public class DurableQueueDeliveryTest
{
    [GenerateSerializer]
    public class TestMessage
    {
        [Id(0)]
        public Guid Id { get; set; }

        [Id(1)]
        public string Value { get; set; } = string.Empty;
    }

    public class DurableQueueTestId : IDurableQueueId
    {
        public string ToRaw() => "test-durable-delivery";
    }

    public class Root : ClusterTestRoot<StateMigrationTest.EmptyPayload>
    {
        public Root(ClusterTestUtils utils) : base(utils)
        {
        }

        public override string Group => TestGroups.Messaging;
        public override string Title => "durable-queue-delivery";

        protected override async Task Run(ClusterTestNodeHandle handle, StateMigrationTest.EmptyPayload payload)
        {
            handle.Progress.SetStatus(OperationStatus.InProgress);

            var queueId = new DurableQueueTestId();
            var messageCount = 10;
            var receivedMessages = new List<TestMessage>();
            var completion = new TaskCompletionSource();

            // Start listening before sending
            await Messaging.ListenDurableQueue<TestMessage>(handle.Lifetime, queueId, message =>
            {
                lock (receivedMessages)
                {
                    receivedMessages.Add(message);

                    if (receivedMessages.Count >= messageCount)
                        completion.TrySetResult();
                }
            });

            handle.Progress.Log("Listener ready, sending messages...");
            handle.Progress.SetProgress(0.2f);

            // Send messages
            for (var i = 0; i < messageCount; i++)
            {
                await Messaging.PushDirectQueue(queueId, new TestMessage
                {
                    Id = Guid.NewGuid(),
                    Value = $"msg-{i}"
                });
            }

            handle.Progress.Log($"Sent {messageCount} messages, waiting for delivery...");
            handle.Progress.SetProgress(0.5f);

            // Wait with timeout
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

            try
            {
                await completion.Task.WaitAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                throw new Exception(
                    $"Timeout: received {receivedMessages.Count}/{messageCount} messages");
            }

            // Verify all messages arrived
            if (receivedMessages.Count != messageCount)
                throw new Exception(
                    $"Message count mismatch: expected {messageCount}, got {receivedMessages.Count}");

            handle.Progress.Log($"All {messageCount} messages delivered");
            handle.Progress.SetProgress(1f);
        }
    }
}
