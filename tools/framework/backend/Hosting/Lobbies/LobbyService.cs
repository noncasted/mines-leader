using Cluster.Lobbies;
using Game.Session;
using Internal;
using Microsoft.Extensions.Logging;
using Shared;

namespace Game.Lobbies;

// Network service "Lobby" of a lobby session (the key and the property ids are shared with the client: SharedLobbyService).
// - Lobby (LobbyState, id 1): written only here - the code of the room and the index of the lobby leader
//   (the session owner; when the leader leaves, the next user by index).
// - Start (LobbyStartState, id 2): written by the client of the leader - "Start" / "Cancel". Cleared here
//   when the leader changes while it is set (its writer is gone) and when the match of the start is not created.
// - Countdown (LobbyCountdownState, id 3): written only here - the countdown that runs while Start is set,
//   one step a second on the clock of the server: every client shows the number it gets.
// - Game (LobbyGameState, id 4): written only here, once, when the countdown reaches zero - the match
//   session with a place reserved for every user of the lobby. From that moment the lobby is sealed:
//   nobody can enter it by the code (the code passes to the match and answers "the game has already
//   started" until the match is closed), the start flag is not read any more.
// - Visibility (LobbyVisibilityState, id 5): written by the client of the leader - "CLOSED / OPEN". A new lobby
//   is closed. Stays when the leader changes.
// The lobby reports itself to the list of open lobbies (ILobbyListings) until it starts its match.
// The lobby of the late players of a running match (LobbySessionInfo.IsRunning, LobbyState.IsRunning) is the same
// service without a leader start: nothing is written by clients, nothing is reported (the match reports itself). When
// the late players get a place in the match is up to the game: a derived service (OnLatePlayersStarted, Admit,
// ClearGame, MarkRunOver); the game adds its own properties from SharedLobbyService.GamePropertyFirst (BindProperty
// in its constructor).
public class LobbyService : Service
{
    public LobbyService(
        ISessionOwner owner,
        ISessionUsers users,
        ISessionAdmission admission,
        ILobbyMatches matches,
        ILobbyListings listings,
        IExecutionQueue executionQueue,
        TimeProvider time,
        LobbySessionInfo info,
        ILogger<LobbyService> logger) : base(SharedLobbyService.Key)
    {
        // The lobby of the late players of a match found by the search: no lobby, no code.
        var isCodeless = info.IsRunning == true && info.LobbySessionId == Guid.Empty;

        if (isCodeless == true ? info.Code.Length != 0 : LobbyCode.IsValid(info.Code) == false)
            throw new InvalidOperationException($"Lobby session is created with an invalid code '{info.Code}'");

        _owner = owner;
        _users = users;
        _matches = matches;
        _executionQueue = executionQueue;
        _info = info;
        _logger = logger;

        _properties = new LobbyProperties(owner, info);

        BindProperty(_properties.Lobby);
        BindProperty(_properties.Start);
        BindProperty(_properties.Countdown);
        BindProperty(_properties.Game);
        BindProperty(_properties.Visibility);

        var matchStart = new LobbyMatchStart(_properties, owner, users, admission, matches, time, info, logger);

        _countdown = new LobbyCountdown(_properties, matchStart, executionQueue, time, CountdownStep, logger);
        _listing = new LobbyListingReporter(_properties, owner, users, listings, info, logger);
    }

    public static readonly TimeSpan CountdownStep = TimeSpan.FromSeconds(1);

    private readonly ISessionOwner _owner;
    private readonly ISessionUsers _users;
    private readonly ILobbyMatches _matches;
    private readonly IExecutionQueue _executionQueue;
    private readonly LobbySessionInfo _info;
    private readonly ILogger<LobbyService> _logger;
    private readonly LobbyProperties _properties;
    private readonly LobbyCountdown _countdown;
    private readonly LobbyListingReporter _listing;

    protected LobbySessionInfo Info => _info;
    protected ISessionUsers Users => _users;
    protected IExecutionQueue ExecutionQueue => _executionQueue;
    protected ILogger Logger => _logger;

    // The match is written to Game: the users are on their way to it.
    protected bool IsGameStarted => _properties.IsGameStarted;

    // Running mode: the match has ended before the late players got in.
    protected bool IsRunOver => _properties.Lobby.Value.IsRunOver;

    protected override void OnStarted(IReadOnlyLifetime lifetime)
    {
        _logger.LogInformation("[Lobby] Lobby code {Code}", _properties.Lobby.Value.Code);

        _owner.Owner.View(lifetime, OnOwnerChanged);

        if (_info.IsRunning == true)
        {
            OnLatePlayersStarted(lifetime);
            return;
        }

        _properties.Start.Written.Advise(lifetime, state => _countdown.OnStartWritten(lifetime, state));
        _listing.Start(lifetime);
    }

    // The lobby of the late players of a running match is started: the game decides when its users get a place in the
    // match (Admit from the session queue, ClearGame when the entry is over, MarkRunOver when the match has ended).
    // By default nobody is admitted from the lobby: a player asks the meta for the match itself (SharedLobbies.JoinMatch).
    protected virtual void OnLatePlayersStarted(IReadOnlyLifetime lifetime)
    {
    }

    // Running mode, in the session queue: a place in the match for the user; the first place writes the match to Game.
    protected void Admit(Guid userId)
    {
        if (_info.IsRunning == false)
            throw new InvalidOperationException("Admit is for the lobby of the late players of a running match");

        var match = _matches.Admit(_info.RunningMatchId, userId);

        if (match == null)
        {
            _logger.LogWarning("{UserId} [Lobby] Match {SessionID} has no place for the late player",
                userId,
                _info.RunningMatchId);

            return;
        }

        if (IsGameStarted == true)
            return;

        _properties.Game.Set(new LobbyGameState
        {
            ServerUrl = match.ServerUrl,
            SessionId = match.SessionId,
            PlayersCount = 1
        });
    }

    // Running mode, in the session queue: the entry into the match is over, the users left in the lobby wait for the
    // next one.
    protected void ClearGame()
    {
        if (IsGameStarted == true)
            _properties.Game.Set(new LobbyGameState());
    }

    // Running mode, in the session queue: the match has ended (won, lost or closed) before the late players got in.
    protected void MarkRunOver()
    {
        _logger.LogInformation("[Lobby] Match {SessionID} is over: the late players go back", _info.RunningMatchId);

        _properties.Lobby.Update(state => state.IsRunOver = true);
        ClearGame();
    }

    private void OnOwnerChanged(IUser? owner)
    {
        // The countdown belongs to the leader that started it. After the match is created the users leave
        // the lobby one by one: nothing to reset.
        if (_properties.Start.Value.IsStarting == true && IsGameStarted == false)
        {
            _logger.LogInformation("[Lobby] The leader has changed during the countdown: the start is reset");

            _countdown.Stop();
            _properties.Start.Set(new LobbyStartState());
        }

        // Nobody is connected: the last index stays, nobody reads it; the next user to join becomes the leader.
        if (owner == null)
            return;

        _logger.LogInformation("[Lobby] Lobby leader: user {Index}", owner.Index);
        _properties.Lobby.Update(state => state.OwnerIndex = owner.Index);
        _listing.Report();
    }
}
