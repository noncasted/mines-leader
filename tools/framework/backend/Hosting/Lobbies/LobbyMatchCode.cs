using Game.Session;

namespace Game.Lobbies;

// The code of a lobby held by its match (ILobbyCodeLeases.PassToMatch), as the rule of what follows the match sees it
// (ILobbyCodeAfterMatch): the lobby, the match and who started it, and what may be done with the code.
public class LobbyMatchCode
{
    internal LobbyMatchCode(
        LobbyCodeLeases leases,
        LobbyCodeLease lease,
        Guid matchSessionId,
        Guid leaderId,
        IReadOnlyList<Guid> userIds)
    {
        _leases = leases;
        _lease = lease;
        MatchSessionId = matchSessionId;
        LeaderId = leaderId;
        UserIds = userIds;
    }

    private readonly LobbyCodeLeases _leases;
    private readonly LobbyCodeLease _lease;

    public string Code => _lease.Code;
    public Guid LobbySessionId => _lease.LobbySessionId;
    public Guid MatchSessionId { get; }

    // The leader and the users of the lobby at the start.
    public Guid LeaderId { get; }
    public IReadOnlyList<Guid> UserIds { get; }

    // True for the first caller only (under the lock of the leases): the first leave and the close of the match may come
    // at the same moment.
    public bool ClaimEnd()
    {
        return _leases.ClaimMatchEnd(_lease);
    }

    // The code goes back to the registry, after the calls queued before.
    public void Release()
    {
        _leases.ReleaseFromMatch(_lease, MatchSessionId);
    }

    // The code goes to the lobby (a lobby session of this server created for it): the registry points the code to it,
    // the lobby holds the code and releases it when it is closed; the match does not touch the code any more.
    public void ReturnTo(ISession lobby)
    {
        _leases.ReturnFromMatch(_lease, lobby);
    }
}
