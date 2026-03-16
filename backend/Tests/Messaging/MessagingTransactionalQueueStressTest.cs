using System.Diagnostics.CodeAnalysis;
using Cluster.Discovery;
using Common.Extensions;
using Common.Reactive;
using Infrastructure;
using Microsoft.Extensions.Logging;

namespace Tests;

public class MessagingTransactionalQueueStressTest
{
    [GenerateSerializer]
    [method: SetsRequiredMembers]
    public class StartPayload()
    {
        [Id(0)]
        public required int MessageCount { get; init; } = 100;

        [Id(1)]
        public required float Delay { get; init; } = 0.01f;
    }

    [GenerateSerializer]
    public class MessagePayload
    {
        [Id(0)]
        public required string Service { get; init; }
    }

    public static string TestName => "messaging-queue-transactional-stress-test";

    public class Root : ClusterTestRoot<StartPayload>
    {
        public Root(ClusterTestUtils utils) : base(utils)
        {
        }

        public override string Group => TestGroups.Messaging;
        public override string Title => "Messaging transactional queue";

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

                Logger.LogInformation("Received message {ReceivedCount}/{TotalMessages} from service {Service}",
                    receivedCount,
                    totalMessages,
                    message.Service
                );

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
        public Node(IOrleans orleans, ClusterTestUtils utils) : base(utils)
        {
            _orleans = orleans;
        }

        private readonly IOrleans _orleans;

        protected override string Name => TestName;

        protected override async Task Run(IReadOnlyLifetime lifetime, StartPayload payload)
        {
            for (var i = 0; i < payload.MessageCount; i++)
            {
                try
                {
                    Logger.LogInformation("Sending message {MessageIndex}/{TotalMessages} from {Service}",
                        i + 1,
                        payload.MessageCount,
                        Environment.Tag.ToString()
                    );

                    await _orleans.InTransaction(() =>
                    {
                        Messaging.PushTransactionalQueue(
                            new MessageQueueId(TestName),
                            new MessagePayload
                            {
                                Service = Environment.Tag.ToString()
                            }
                        );

                        return Task.CompletedTask;
                    });

                    Logger.LogInformation("Successfully sent message {MessageIndex}/{TotalMessages} from {Service}",
                        i + 1,
                        payload.MessageCount,
                        Environment.Tag.ToString()
                    );

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