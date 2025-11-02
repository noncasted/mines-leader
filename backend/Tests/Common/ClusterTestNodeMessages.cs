using Cluster.Discovery;
using Infrastructure;

namespace Tests;

public static class ClusterTestNodeMessages
{
    [GenerateSerializer]
    public class StartRequest
    {
        [Id(0)] public required ServiceTag Service { get; init; }
        [Id(1)] public required string NodeName { get; init; }
        [Id(2)] public object? Payload { get; set; }
    }

    [GenerateSerializer]
    public class StartResponse
    {
    }

    [GenerateSerializer]
    public class Terminate
    {
    }

    public class PipeId : IMessagePipeId
    {
        public PipeId(ServiceTag service, string name, string target)
        {
            _service = service;
            _name = name;
            _target = target;
        }

        private readonly ServiceTag _service;
        private readonly string _name;
        private readonly string _target;

        public string ToRaw()
        {
            return $"cluster-test-node-{_service.ToString()}-{_name}-{_target}";
        }
    }
}