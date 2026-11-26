using Cluster.Lobbies;
using Game.Session;
using Internal;
using Microsoft.Extensions.Logging;

namespace Game.Lobbies;

// Backend-only service of a match started by a lobby: reports the match to the list of open lobbies by the code of
// its lobby (ILobbyListings) - players and owner. No properties: clients never fetch it.
// When a running match can be entered is decided by the game (ILobbyEntryPolicy), not by the listing: a late player
// waits in the lobby of the late players, a match that is over answers NotFound.
public class MatchListingService : Service
{
    public MatchListingService(
        ISessionOwner owner,
        ISessionUsers users,
        ILobbyListings listings,
        MatchListingInfo info,
        ILogger<MatchListingService> logger) : base(ServiceKey)
    {
        if (info.LobbySessionId == Guid.Empty)
            throw new InvalidOperationException("Match listing is created for a match without a lobby");

        _owner = owner;
        _users = users;
        _listings = listings;
        _info = info;
        _logger = logger;
    }

    public const string ServiceKey = "MatchListing";

    private readonly ISessionOwner _owner;
    private readonly ISessionUsers _users;
    private readonly ILobbyListings _listings;
    private readonly MatchListingInfo _info;
    private readonly ILogger<MatchListingService> _logger;

    // Runs in the session queue, like everything below.
    protected override void OnStarted(IReadOnlyLifetime lifetime)
    {
        _logger.LogInformation("[Lobby] Match of lobby {LobbySessionID} is listed: {IsOpen}", _info.LobbySessionId, _info.IsOpen);

        Report();
        _owner.Owner.Advise(lifetime, (_, _) => Report());

        _users.ViewChanges(lifetime, Report);
    }

    private void Report()
    {
        var owner = _owner.Owner.Value;

        _listings.Report(_info.LobbySessionId, new LobbyListing
        {
            IsOpen = _info.IsOpen,
            LeaderUserId = owner?.BackendId ?? Guid.Empty,
            PlayersCount = _users.CountPlayers(),
            MaxPlayers = _info.MaxUsers,
            IsRunning = true
        });
    }
}
