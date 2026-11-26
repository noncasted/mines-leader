using Cluster.Deploy;
using Cluster.Monitoring;
using Common.Extensions;

namespace Cluster.Discovery;

public interface IGameServerBalancer
{
    // The game server for a new session: the one with the fewest live sessions; a tie - a random one of them.
    GameServerOverview Pick();
}

// Sessions are counted from LiveSessionsData (every game gateway writes its slice under its service id when a
// session is added or removed). A server without a slice yet has no sessions. A burst of requests faster than the
// slice update goes to one server - the count catches up with the next update.
public class GameServerBalancer : IGameServerBalancer
{
    public GameServerBalancer(IServiceDiscovery discovery, IDeploymentState<LiveSessionsData> liveSessions)
    {
        _discovery = discovery;
        _liveSessions = liveSessions;
    }

    private readonly IServiceDiscovery _discovery;
    private readonly IDeploymentState<LiveSessionsData> _liveSessions;

    public GameServerOverview Pick()
    {
        var slices = _liveSessions.Value.Slices;
        var candidates = new List<GameServerOverview>();
        var fewest = int.MaxValue;

        // A crashed server stays a member until StaleThreshold and, with no sessions left, would be the first pick.
        // The update times come from the clock of the discovery storage: compared with each other, not with the local
        // clock.
        var entries = _discovery.Entries.Values.ToArray();
        var newest = entries.Length == 0 ? DateTime.MinValue : entries.Max(entry => entry.UpdateTime);

        foreach (var server in entries.OfType<GameServerOverview>())
        {
            if (newest - server.UpdateTime > ServiceDiscoveryStorage.GameServerHeartbeatTimeout)
                continue;

            var count = slices.TryGetValue(server.Id, out var sessions) == true ? sessions.Count : 0;

            if (count > fewest)
                continue;

            if (count < fewest)
            {
                fewest = count;
                candidates.Clear();
            }

            candidates.Add(server);
        }

        if (candidates.Count == 0)
            throw new InvalidOperationException("No game servers available");

        return candidates.Random();
    }
}
