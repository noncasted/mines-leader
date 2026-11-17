using Newtonsoft.Json;

namespace Cluster.Monitoring.Online;

// One UTC hour of a day. The ids of the users are kept only while the hour is open, so a user who comes back in
// the same hour (also after a restart of the coordinator) is not counted twice; a closed hour keeps the count.
[GenerateSerializer]
public class OnlineHourState
{
    [Id(0)] [JsonProperty] public int Hour { get; private set; }
    [Id(1)] [JsonProperty] public int Peak { get; private set; }
    [Id(2)] [JsonProperty] public int UniqueUsers { get; private set; }
    [Id(3)] [JsonProperty] public bool IsClosed { get; private set; }
    [Id(4)] [JsonProperty] public HashSet<Guid> Users { get; private set; } = new();

    public static OnlineHourState Open(int hour)
    {
        return new OnlineHourState { Hour = hour };
    }

    public void Apply(IReadOnlySet<Guid> online)
    {
        if (IsClosed == true)
            throw new InvalidOperationException($"Online hour {Hour} is closed");

        Peak = Math.Max(Peak, online.Count);
        Users.UnionWith(online);
        UniqueUsers = Users.Count;
    }

    public void Close()
    {
        IsClosed = true;
        Users.Clear();
    }

    // The same hour counted by another coordinator: a closed hour has lost its ids, so its count is a lower bound.
    public OnlineHourState MergedWith(OnlineHourState other)
    {
        if (other.Hour != Hour)
            throw new InvalidOperationException($"Online hour {Hour} merged with hour {other.Hour}");

        var isClosed = IsClosed || other.IsClosed;
        var users = isClosed ? new HashSet<Guid>() : new HashSet<Guid>(Users.Concat(other.Users));
        var uniqueUsers = isClosed ? Math.Max(UniqueUsers, other.UniqueUsers) : users.Count;

        return new OnlineHourState
        {
            Hour = Hour,
            Peak = Math.Max(Peak, other.Peak),
            UniqueUsers = uniqueUsers,
            IsClosed = isClosed,
            Users = users
        };
    }

    public OnlineHourState Copy()
    {
        return new OnlineHourState
        {
            Hour = Hour,
            Peak = Peak,
            UniqueUsers = UniqueUsers,
            IsClosed = IsClosed,
            Users = new HashSet<Guid>(Users)
        };
    }
}
