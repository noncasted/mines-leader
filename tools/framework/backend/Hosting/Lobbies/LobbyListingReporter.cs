using Cluster.Lobbies;
using Game.Session;
using Internal;
using Microsoft.Extensions.Logging;
using Shared;

namespace Game.Lobbies;

// The lobby in the list of open lobbies (ILobbyListings), until it starts its match: from then on the match reports
// the code.
internal class LobbyListingReporter
{
    public LobbyListingReporter(
        LobbyProperties properties,
        ISessionOwner owner,
        ISessionUsers users,
        ILobbyListings listings,
        LobbySessionInfo info,
        ILogger logger)
    {
        _properties = properties;
        _owner = owner;
        _users = users;
        _listings = listings;
        _info = info;
        _logger = logger;
    }

    private readonly LobbyProperties _properties;
    private readonly ISessionOwner _owner;
    private readonly ISessionUsers _users;
    private readonly ILobbyListings _listings;
    private readonly LobbySessionInfo _info;
    private readonly ILogger _logger;

    public void Start(IReadOnlyLifetime lifetime)
    {
        _properties.Visibility.Written.Advise(lifetime, OnVisibilityWritten);

        _users.ViewChanges(lifetime, Report);
    }

    public void Report()
    {
        if (_info.IsRunning == true || _properties.IsGameStarted == true)
            return;

        var owner = _owner.Owner.Value;

        _listings.Report(_info.SessionId, new LobbyListing
        {
            IsOpen = _properties.Visibility.Value.IsOpen,
            LeaderUserId = owner?.BackendId ?? Guid.Empty,
            PlayersCount = _users.CountPlayers(),
            MaxPlayers = _info.MaxUsers,
            IsRunning = false
        });
    }

    private void OnVisibilityWritten(LobbyVisibilityState state)
    {
        _logger.LogInformation("[Lobby] Lobby {Code} is {Visibility}", _properties.Lobby.Value.Code, state.IsOpen ? "open" : "closed");
        Report();
    }
}
