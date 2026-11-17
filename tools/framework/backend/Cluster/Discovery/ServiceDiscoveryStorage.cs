using Infrastructure.State;
using Microsoft.Extensions.Logging;

namespace Cluster.Discovery;

public class ServiceDiscoveryStorage : Grain, IServiceDiscoveryStorage
{
    public ServiceDiscoveryStorage(
        [State] State<ServiceDiscoveryStorageState> state,
        ILogger<ServiceDiscoveryStorage> logger)
    {
        _state = state;
        _logger = logger;
    }

    private readonly State<ServiceDiscoveryStorageState> _state;
    private readonly ILogger<ServiceDiscoveryStorage> _logger;

    // A member without a heartbeat for this long is treated as gone (also by ServiceSlicesWriter).
    public static readonly TimeSpan StaleThreshold = TimeSpan.FromSeconds(30);

    // A game server without a heartbeat for this long gets no new sessions (GameServerBalancer); the members refresh
    // every 2 s.
    public static readonly TimeSpan GameServerHeartbeatTimeout = TimeSpan.FromSeconds(10);

    public async Task<Dictionary<Guid, IServiceOverview>> Update(IServiceOverview overview)
    {
        var deployId = this.GetPrimaryKey();
        var now = DateTime.UtcNow;
        var staleCutoff = now - StaleThreshold;

        var value = await _state.Update(s => {
            // Stamped with the clock of this grain: the staleness of the members is measured by one clock, not by
            // the clocks of their hosts.
            s.Set(Stamp(overview, now));

            var staleIds = s.Members.Values
                            .Where(m => m.UpdateTime < staleCutoff)
                            .Select(m => m.Id)
                            .ToList();

            foreach (var id in staleIds)
            {
                var member = s.Members[id];
                s.Remove(id);

                _logger.LogInformation(
                    "[ServiceDiscoveryStorage] Removed stale member {MemberId} (tag={Tag}, lastUpdate={LastUpdate}) from deploy {DeployId}",
                    id, member.Tag, member.UpdateTime, deployId);
            }
        });

        return value.Members.ToDictionary();
    }

    private static IServiceOverview Stamp(IServiceOverview overview, DateTime now)
    {
        return overview switch
        {
            GameServerOverview server => new GameServerOverview
            {
                Id = server.Id,
                Tag = server.Tag,
                UpdateTime = now,
                Url = server.Url
            },
            ServiceOverview service => new ServiceOverview
            {
                Id = service.Id,
                Tag = service.Tag,
                UpdateTime = now
            },
            _ => throw new InvalidOperationException($"Unknown service overview {overview.GetType().FullName}")
        };
    }

    public async Task<Dictionary<Guid, IServiceOverview>> Get()
    {
        var value = await _state.ReadValue();
        return value.Members.ToDictionary();
    }

    public async Task Unregister(Guid serviceId)
    {
        await _state.Write(s => s.Remove(serviceId));

        _logger.LogInformation("[ServiceDiscoveryStorage] Unregistered member {MemberId} from deploy {DeployId}",
            serviceId, this.GetPrimaryKey());
    }
}