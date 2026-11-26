using Cluster.Deploy;
using Common.Extensions;
using Infrastructure;
using Microsoft.Extensions.Hosting;

namespace Cluster.Lobbies;

// Access to the lobby registry of the current deploy from services (game servers and the meta).
public interface ILobbyCodes
{
    Task<string> Register(Guid serverId, Guid sessionId);
    Task<LobbyRegistryEntry?> Find(string code);
    Task MarkStarted(string code, Guid sessionId, Guid matchSessionId);

    // The code of sessionId (its lobby, started or not) now leads to the lobby returnSessionId on the same server, not
    // started. A call of anybody but the holder changes nothing: it throws, as Release.
    Task MarkReturned(string code, Guid sessionId, Guid returnSessionId);

    Task Release(string code, Guid sessionId);
    Task UpdateListing(string code, Guid sessionId, LobbyListing listing);
    Task<OpenLobbies> GetOpenLobbies();
}

public class LobbyCodes : ILobbyCodes
{
    public LobbyCodes(IOrleans orleans, IDeployContext deployContext)
    {
        _orleans = orleans;
        _deployContext = deployContext;
    }

    private readonly IOrleans _orleans;
    private readonly IDeployContext _deployContext;

    public Task<string> Register(Guid serverId, Guid sessionId)
    {
        return GetRegistry().Register(serverId, sessionId);
    }

    public Task<LobbyRegistryEntry?> Find(string code)
    {
        return GetRegistry().Find(code);
    }

    public Task MarkStarted(string code, Guid sessionId, Guid matchSessionId)
    {
        return GetRegistry().MarkStarted(code, sessionId, matchSessionId);
    }

    public Task MarkReturned(string code, Guid sessionId, Guid returnSessionId)
    {
        return GetRegistry().MarkReturned(code, sessionId, returnSessionId);
    }

    public Task Release(string code, Guid sessionId)
    {
        return GetRegistry().Release(code, sessionId);
    }

    public Task UpdateListing(string code, Guid sessionId, LobbyListing listing)
    {
        return GetRegistry().UpdateListing(code, sessionId, listing);
    }

    public Task<OpenLobbies> GetOpenLobbies()
    {
        return GetRegistry().GetOpenLobbies();
    }

    private ILobbyRegistry GetRegistry()
    {
        var deployId = _deployContext.DeployId;

        if (deployId == Guid.Empty)
            throw new InvalidOperationException("Lobby registry is requested before the deploy id is known");

        return _orleans.GetGrain<ILobbyRegistry>(deployId);
    }
}
