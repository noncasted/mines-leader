using System.Diagnostics.CodeAnalysis;
using Cluster.Discovery;
using Common.Extensions;
using Common.Reactive;
using Infrastructure;
using Microsoft.Extensions.Logging;

namespace Tests;

public class MessagePipeSendResponseStressTest
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
    public class RequestPayload
    {
        [Id(0)]
        public required string Service { get; init; }

        [Id(1)]
        public required int MessageIndex { get; init; }
    }

    [GenerateSerializer]
    public class ResponsePayload
    {
        [Id(0)]
        public required string Message { get; init; }

        [Id(1)]
        public required long Timestamp { get; init; }

        [Id(2)]
        public required string ProcessedBy { get; init; }
    }

    public static string TestName => "messaging-pipe-send-response-stress-test";

    public class Root : ClusterTestRoot<StartPayload>
    {
        public Root(ClusterTestUtils utils) : base(utils)
        {
        }

        public override string Group => TestGroups.Messaging;
        public override string Title => "Messaging pipe send with response (request-response)";

        protected override async Task Run(ClusterTestNodeHandle handle, StartPayload payload)
        {
            var completion = new TaskCompletionSource();
            var totalMessages = payload.MessageCount * 5;
            var processedCount = 0;

            handle.Progress.Log("Setting up request-response handler...");

            await Messaging.AddPipeRequestHandler<RequestPayload, ResponsePayload>(
                handle.Lifetime,
                new MessagePipeId(TestName),
                OnRequest
            );

            handle.Progress.SetStatus(OperationStatus.InProgress);
            handle.Progress.Log("Starting test nodes...");

            await Task.WhenAll(
                handle.StartNode(ServiceTag.Game, TestName, payload),
                handle.StartNode(ServiceTag.Meta, TestName, payload),
                handle.StartNode(ServiceTag.Coordinator, TestName, payload),
                handle.StartNode(ServiceTag.Silo, TestName, payload),
                handle.StartNode(ServiceTag.Console, TestName, payload)
            );

            await completion.Task;

            return;

            async Task<ResponsePayload> OnRequest(RequestPayload request)
            {
                Interlocked.Increment(ref processedCount);

                Logger.LogInformation(
                    "Processed request {ProcessedCount}/{TotalMessages} from service {Service} at index {MessageIndex}",
                    processedCount,
                    totalMessages,
                    request.Service,
                    request.MessageIndex
                );

                var progressValue = (float)processedCount / totalMessages;
                handle.Progress.SetProgress(progressValue);
                handle.Progress.Log($"Processed {processedCount}/{totalMessages} requests");

                var response = new ResponsePayload
                {
                    Message = $"Processed request {request.MessageIndex} from {request.Service}",
                    Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    ProcessedBy = Environment.Tag.ToString()
                };

                if (processedCount >= totalMessages)
                {
                    // Delay slightly to ensure all responses are processed
                    await Task.Delay(100);
                    completion.SetResult();
                }

                return response;
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
                    Logger.LogInformation(
                        "Sending request-response message {MessageIndex}/{TotalMessages} from {Service}",
                        i + 1,
                        payload.MessageCount,
                        Environment.Tag.ToString()
                    );

                    var response = await Messaging.SendPipe<ResponsePayload>(
                        new MessagePipeId(TestName),
                        new RequestPayload
                        {
                            Service = Environment.Tag.ToString(),
                            MessageIndex = i + 1
                        }
                    );

                    Logger.LogInformation(
                        "Successfully received response for message {MessageIndex}/{TotalMessages}: {ResponseMessage}",
                        i + 1,
                        payload.MessageCount,
                        response.Message
                    );

                    await Task.Delay(TimeSpan.FromSeconds(payload.Delay));
                }
                catch (Exception e)
                {
                    Logger.LogError(
                        e,
                        "Failed to send request-response message {MessageIndex}/{TotalMessages}",
                        i + 1,
                        payload.MessageCount
                    );
                }
            }
        }
    }
}