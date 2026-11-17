using Cluster.Deploy;
using Common;
using Infrastructure.State;
using Shared;

namespace Cluster.Monitoring.Online;

// The online statistics as the console shows them, written by the OnlineTracker of the coordinator and read on any
// host through IDeploymentState<OnlineStatsLiveData>. A projection of the OnlineDayState rows: deploy-scoped, the
// coordinator of the next deploy writes it again from the rows. Users are counted by their id, so a user on two
// gateways or in two tabs is one. Periods are UTC, the oldest first, the last one is the current hour / day.
[GenerateSerializer]
[GrainState(Table = "cluster", State = "deployment_state_OnlineStatsLiveData", Lookup = "OnlineStatsLive", Key = GrainKeyType.Guid)]
public class OnlineStatsLiveData : IDirectStateValue, IDeployScopedState
{
    // Users online now; the console that wants it live without the flush delay counts the distinct ids of
    // ConnectedUsersLiveData itself.
    [Id(0)] public int Current { get; init; }
    [Id(1)] public DateTime UpdatedAt { get; init; }
    [Id(2)] public List<OnlinePeriod> Hours { get; init; } = new();
    [Id(3)] public List<OnlinePeriod> Days { get; init; } = new();
    [Id(4)] public int UniqueLast7Days { get; init; }
    [Id(5)] public int UniqueLast30Days { get; init; }

    public int Version => 0;
}
