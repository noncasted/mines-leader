using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Cluster.Discovery;
using Common.Extensions;
using Common.Reactive;
using Infrastructure;
using Microsoft.Extensions.Logging;

namespace Benchmarks;

public class RuntimeChannelSendStressTest
{
    [GenerateSerializer]
    [method: SetsRequiredMembers]
    public class StartPayload()
    {
        [Id(0)]
        public required int MessageCount { get; init; } = 100;

        [Id(1)]
        public required float Delay { get; init; } = 0.001f;
    }

    [GenerateSerializer]
    public class MessagePayload
    {
        [Id(0)]
        public required string Service { get; init; }

        [Id(1)]
        public required int MessageIndex { get; init; }
    }

    public static string TestName => "runtime-channel-send-stress-test";

    public class Root : ClusterTestRoot<StartPayload>
    {
        public Root(ClusterTestUtils utils, BenchmarkStorage benchmarkStorage) : base(utils, benchmarkStorage)
        {
        }

        public override string Group => TestGroups.Messaging;
        public override string Title => "RuntimeChannel send (one-way broadcast)";
        public override string MetricName => "msg/s";

        protected override async Task Run(ClusterTestNodeHandle handle, StartPayload payload)
        {
            var completion = new TaskCompletionSource();
            var totalMessages = payload.MessageCount * 5;
            var receivedCount = 0;

            handle.Progress.Log("Setting up channel listeners...");

            await Messaging.ListenChannel<MessagePayload>(
                handle.Lifetime,
                new RuntimeChannelId(TestName),
                OnMessage
            );

            handle.Progress.SetStatus(OperationStatus.InProgress);
            handle.Progress.Log("Starting test nodes...");

            var stopwatch = Stopwatch.StartNew();

            await Task.WhenAll(
                handle.StartNode(ServiceTag.Game, TestName, payload),
                handle.StartNode(ServiceTag.Meta, TestName, payload),
                handle.StartNode(ServiceTag.Coordinator, TestName, payload),
                handle.StartNode(ServiceTag.Silo, TestName, payload),
                handle.StartNode(ServiceTag.Console, TestName, payload)
            );

            await completion.Task;
            stopwatch.Stop();

            handle.ReportMetric(totalMessages / stopwatch.Elapsed.TotalSeconds);

            return;

            void OnMessage(MessagePayload message)
            {
                Interlocked.Increment(ref receivedCount);

                Logger.LogInformation(
                    "Received message {ReceivedCount}/{TotalMessages} from service {Service} at index {MessageIndex}",
                    receivedCount,
                    totalMessages,
                    message.Service,
                    message.MessageIndex
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
                    Logger.LogInformation(
                        "Publishing message {MessageIndex}/{TotalMessages} from {Service}",
                        i + 1,
                        payload.MessageCount,
                        Environment.Tag.ToString()
                    );

                    await Messaging.PublishChannel(
                        new RuntimeChannelId(TestName),
                        new MessagePayload
                        {
                            Service = Environment.Tag.ToString(),
                            MessageIndex = i + 1
                        }
                    );

                    Logger.LogInformation(
                        "Successfully published message {MessageIndex}/{TotalMessages} from {Service}",
                        i + 1,
                        payload.MessageCount,
                        Environment.Tag.ToString()
                    );

                    await Task.Delay(TimeSpan.FromSeconds(payload.Delay));
                }
                catch (Exception e)
                {
                    Logger.LogError(
                        e,
                        "Failed to publish message {MessageIndex}/{TotalMessages}",
                        i + 1,
                        payload.MessageCount
                    );
                }
            }
        }
    }
}
