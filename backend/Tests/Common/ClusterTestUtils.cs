using Cluster.Discovery;
using Infrastructure;
using Microsoft.Extensions.Logging;

namespace Tests;

public class ClusterTestUtils
{
    public ClusterTestUtils(IMessaging messaging, IServiceEnvironment environment, ILogger<ClusterTestUtils> logger)
    {
        Messaging = messaging;
        Environment = environment;
        Logger = logger;
    }

    public readonly IMessaging Messaging;
    public readonly IServiceEnvironment Environment;
    public readonly ILogger<ClusterTestUtils> Logger;

    public Task StartNode(ServiceTag service, string nodeName, object? payload = null)
    {
        var pipeId = new ClusterTestNodeMessages.NodePipeId(service, nodeName);

        var request = new ClusterTestNodeMessages.StartRequest
        {
            Service = service,
            NodeName = nodeName,
            Payload = payload
        };

        return Messaging.SendPipe<ClusterTestNodeMessages.StartResponse>(pipeId, request);
    }

    public Task TerminateNode(ServiceTag service, string nodeName)
    {
        var channelId = new ClusterTestNodeMessages.NodeChannelId(service, nodeName);
        return Messaging.PublishChannel(channelId, new ClusterTestNodeMessages.Terminate());
    }
}
