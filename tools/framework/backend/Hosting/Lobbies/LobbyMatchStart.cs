using Game.Session;
using Microsoft.Extensions.Logging;
using Shared;

namespace Game.Lobbies;

// Zero of the countdown: the lobby is sealed and its match is created with a place for everybody the lobby has let in.
internal class LobbyMatchStart
{
    public LobbyMatchStart(
        LobbyProperties properties,
        ISessionOwner owner,
        ISessionUsers users,
        ISessionAdmission admission,
        ILobbyMatches matches,
        TimeProvider time,
        LobbySessionInfo info,
        ILogger logger)
    {
        _properties = properties;
        _owner = owner;
        _users = users;
        _admission = admission;
        _matches = matches;
        _time = time;
        _info = info;
        _logger = logger;
    }

    private readonly LobbyProperties _properties;
    private readonly ISessionOwner _owner;
    private readonly ISessionUsers _users;
    private readonly ISessionAdmission _admission;
    private readonly ILobbyMatches _matches;
    private readonly TimeProvider _time;
    private readonly LobbySessionInfo _info;
    private readonly ILogger _logger;

    public void Start()
    {
        // Sealed first: nobody can take a place in the lobby after the list of its users is read.
        _admission.Seal();

        // Everybody the lobby has let in: the connected users and those still on their way to it (a place
        // reserved by the code a moment ago) - the latter read the address as soon as they connect. A user is listed
        // once, whatever number of its connections are joined (a stale one is being dropped); a bot has no place.
        var userIds = _users.GetPlayerIds().ToList();

        foreach (var userId in _admission.GetPendingUsers(_time.GetUtcNow().UtcDateTime))
        {
            if (userIds.Contains(userId) == false)
                userIds.Add(userId);
        }

        var match = CreateMatch(userIds);

        _logger.LogInformation("[Lobby] Lobby {Code} starts the match {SessionID} for {Count} users",
            _properties.Lobby.Value.Code,
            match.SessionId,
            userIds.Count);

        _properties.Game.Set(new LobbyGameState
        {
            ServerUrl = match.ServerUrl,
            SessionId = match.SessionId,
            PlayersCount = userIds.Count
        });
    }

    // The countdown belongs to the leader that started it (a change of the leader resets it): the leader is here.
    private Guid GetLeaderId()
    {
        var leader = _owner.Owner.Value
                     ?? throw new InvalidOperationException($"Lobby {_info.SessionId} starts its match without a leader");

        return leader.BackendId;
    }

    private LobbyMatch CreateMatch(IReadOnlyList<Guid> userIds)
    {
        try
        {
            return _matches.Create(_info.SessionId, GetLeaderId(), userIds, _properties.Visibility.Value.IsOpen);
        }
        catch (Exception)
        {
            // No match: the lobby goes back to where it was before the start, the leader may start again. The failure
            // itself goes on to the session queue.
            _admission.Unseal();
            _properties.Countdown.Set(new LobbyCountdownState());
            _properties.Start.Set(new LobbyStartState());
            throw;
        }
    }
}
