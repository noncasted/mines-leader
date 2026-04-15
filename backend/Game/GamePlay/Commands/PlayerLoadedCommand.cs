using Game.Session;
using Shared;

namespace Game.GamePlay;

public class PlayerLoadedCommand : Command<MatchActionContexts.PlayerLoaded>
{
    public PlayerLoadedCommand(IPlayersReadyAwaiter playersReadyAwaiter)
    {
        _playersReadyAwaiter = playersReadyAwaiter;
    }

    private readonly IPlayersReadyAwaiter _playersReadyAwaiter;

    protected override void Execute(IUser user, MatchActionContexts.PlayerLoaded context)
    {
        _playersReadyAwaiter.OnPlayerLoaded(user.Id);
    }
}