using Infrastructure;

namespace Cluster.Lobbies;

// Snapshots of the open lobbies of a deploy (OpenLobbies), the full list on every change.
public class OpenLobbiesChannelId : IRuntimeChannelId
{
    public OpenLobbiesChannelId(Guid deployId)
    {
        _deployId = deployId;
    }

    private readonly Guid _deployId;

    public string ToRaw()
    {
        return $"open-lobbies-{_deployId:N}";
    }
}
