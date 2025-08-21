using Shared;

namespace Game.GamePlay;

public class PlayerReadyCommand : Command<MatchActionContexts.PlayerReady>
{
    public PlayerReadyCommand(IGameReadyAwaiter readyAwaiter)
    {
        _readyAwaiter = readyAwaiter;
    }

    private readonly IGameReadyAwaiter _readyAwaiter;

    protected override void Execute(IUser user, MatchActionContexts.PlayerReady context)
    {
        _readyAwaiter.OnPlayerReady(user.Id);
    }
}