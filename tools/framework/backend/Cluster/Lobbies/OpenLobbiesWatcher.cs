using Common.Extensions;
using Cluster.Deploy;
using Infrastructure;
using Internal;
using Microsoft.Extensions.Logging;

namespace Cluster.Lobbies;

// The open lobbies of the current deploy on this host: the snapshot of the registry, then every change it pushes on
// OpenLobbiesChannelId. A snapshot older than the one held (a late push, the answer to the first read) is dropped by
// its version.
public interface IOpenLobbiesWatcher
{
    IViewableProperty<OpenLobbies> Lobbies { get; }
}

public class OpenLobbiesWatcher : IOpenLobbiesWatcher, IDeployAware
{
    public OpenLobbiesWatcher(IOrleans orleans, IMessaging messaging, ILogger<OpenLobbiesWatcher> logger)
    {
        _orleans = orleans;
        _messaging = messaging;
        _logger = logger;
    }

    private readonly IOrleans _orleans;
    private readonly IMessaging _messaging;
    private readonly ILogger<OpenLobbiesWatcher> _logger;
    private readonly ViewableProperty<OpenLobbies> _lobbies = new(Empty());
    private readonly Lock _lock = new();

    // The deploy whose snapshots are applied, changed under _lock: a reload of the previous deploy still in flight
    // after the switch is dropped (the registry of the new deploy counts its versions from 0 again).
    private Guid _deployId;

    public IViewableProperty<OpenLobbies> Lobbies => _lobbies;

    public async Task OnDeployChanged(Guid newDeployId, IReadOnlyLifetime deployLifetime)
    {
        lock (_lock)
        {
            _deployId = newDeployId;
            _lobbies.Set(Empty());
        }

        // Listened before the read: a change made in between comes by the channel, the read never hides it. A gap of
        // the channel (missed pushes) reads the registry again; the version keeps the newer snapshot.
        await _messaging.ListenChannel<OpenLobbies>(deployLifetime, new OpenLobbiesChannelId(newDeployId),
            lobbies => Apply(newDeployId, lobbies),
            () => Reload(newDeployId).NoAwait());

        var current = await Reload(newDeployId);

        _logger.LogInformation("[Lobby] Open lobbies of deploy {DeployId}: {Count}", newDeployId, current.Entries.Count);
    }

    private async Task<OpenLobbies> Reload(Guid deployId)
    {
        var current = await _orleans.GetGrain<ILobbyRegistry>(deployId).GetOpenLobbies();
        Apply(deployId, current);
        return current;
    }

    private void Apply(Guid deployId, OpenLobbies lobbies)
    {
        lock (_lock)
        {
            if (deployId != _deployId || lobbies.Version <= _lobbies.Value.Version)
                return;

            _lobbies.Set(lobbies);
        }
    }

    // Older than any snapshot of the registry (its versions start from 0).
    private static OpenLobbies Empty()
    {
        return new OpenLobbies
        {
            Version = -1
        };
    }
}
