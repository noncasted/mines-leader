using Infrastructure;

namespace Cluster.Coordination;

public class CoordinatorEvents
{
    public static readonly IRuntimeChannelId ReadyId = new ReadyChannelId();

    public class ReadyChannelId : IRuntimeChannelId
    {
        public string ToRaw() => "coordinator-ready";
    }

    [GenerateSerializer]
    public class ReadyPayload
    {
    }
}