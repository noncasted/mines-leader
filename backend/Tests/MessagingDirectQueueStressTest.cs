using Cluster.Discovery;
using Common.Extensions;
using Common.Reactive;
using Infrastructure;
using Microsoft.Extensions.Logging;

namespace Tests;

public class MessagingDirectQueueStressTest
{
    [GenerateSerializer]
    public class StartPayload
    {
        [Id(0)]
        public required int MessageCount { get; init; }

        [Id(1)]
        public required float Delay { get; init; }
    }

    [GenerateSerializer]
    public class MessagePayload
    {
    }

    public static string TestName => "messaging-queue-direct-stress-test";

    public class Root : ClusterTestRoot<StartPayload>
    {
        public Root(ClusterTestUtils utils) : base(utils)
        {
        }

        protected override string Name => TestName;

        protected override async Task Run(ClusterTestNodeHandle handle, StartPayload payload)
        {
            var completion = new TaskCompletionSource();
            var totalMessages = payload.MessageCount * 5;
            var receivedCount = 0;

            handle.Progress.Log("Listening for messages...");
            
            await Messaging.ListenQueue<MessagePayload>(handle.Lifetime, new MessageQueueId(TestName), OnMessage);
            
            handle.Progress.SetStatus(OperationStatus.InProgress);
            handle.Progress.Log("Starting test node...");

            await Task.WhenAll(
                handle.StartNode(ServiceTag.Game, TestName, payload),
                handle.StartNode(ServiceTag.Meta, TestName, payload),
                handle.StartNode(ServiceTag.Coordinator, TestName, payload),
                handle.StartNode(ServiceTag.Silo, TestName, payload),
                handle.StartNode(ServiceTag.Console, TestName, payload)
            );

            await completion.Task;

            return;

            void OnMessage(MessagePayload message)
            {
                Interlocked.Increment(ref receivedCount);

                var progressValue = (float)receivedCount / totalMessages;
                handle.Progress.SetProgress(progressValue);
                handle.Progress.Log($"Received {receivedCount}/{totalMessages} messages");

                if (receivedCount >= totalMessages)
                    completion.SetResult();
            }
        }
    }

    public class Node : ClusterTestNode<StartPayload>
    {
        public Node(ClusterTestUtils utils) : base(utils)
        {
        }

        protected override string Name => TestName;

        protected override async Task Run(IReadOnlyLifetime lifetime, StartPayload payload)
        {
            for (var i = 0; i < payload.MessageCount; i++)
            {
                try
                {
                    await Messaging.PushDirectQueue(new MessageQueueId(TestName), new MessagePayload());
                    await Task.Delay(TimeSpan.FromSeconds(payload.Delay));
                }
                catch (Exception e)
                {
                    Logger.LogError(e, "Failed to send message {MessageIndex}/{TotalMessages}",
                        i + 1,
                        payload.MessageCount
                    );
                }
            }
        }
    }
}