using Game.Session;
using Internal;
using Shared;

namespace Game.Lobbies;

// The properties of the lobby service with their writers (see LobbyService).
internal class LobbyProperties
{
    public LobbyProperties(ISessionOwner owner, LobbySessionInfo info)
    {
        Lobby = new ValueProperty<LobbyState>(SharedLobbyService.LobbyPropertyId, new LobbyState
        {
            Code = info.Code,
            IsRunning = info.IsRunning
        });

        // One writer for every property: the start flag and the visibility - the client of the current leader (of
        // a lobby that has a start), the rest - only the backend (SetPropertyCommand drops the writes of anybody
        // else).
        Lobby.RestrictWriter(_ => false);
        Start.RestrictWriter(user => info.IsRunning == false && user == owner.Owner.Value);
        Countdown.RestrictWriter(_ => false);
        Game.RestrictWriter(_ => false);
        Visibility.RestrictWriter(user => info.IsRunning == false && user == owner.Owner.Value);
    }

    public ValueProperty<LobbyState> Lobby { get; }
    public ValueProperty<LobbyStartState> Start { get; } = new(SharedLobbyService.StartPropertyId);
    public ValueProperty<LobbyCountdownState> Countdown { get; } = new(SharedLobbyService.CountdownPropertyId);
    public ValueProperty<LobbyGameState> Game { get; } = new(SharedLobbyService.GamePropertyId);
    public ValueProperty<LobbyVisibilityState> Visibility { get; } = new(SharedLobbyService.VisibilityPropertyId);

    public bool IsGameStarted => Game.Value.SessionId != Guid.Empty;
}
