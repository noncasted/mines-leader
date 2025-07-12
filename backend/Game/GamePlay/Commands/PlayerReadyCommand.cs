using Shared;

namespace Game.GamePlay;

public class PlayerReadyCommand : Command<PlayerReadyContext>
{
    public PlayerReadyCommand(IGameReadyAwaiter readyAwaiter)
    {
        _readyAwaiter = readyAwaiter;
    }

    private readonly IGameReadyAwaiter _readyAwaiter;

    protected override void Execute(IUser user, PlayerReadyContext context)
    {
        _readyAwaiter.OnPlayerReady(user.Id);
    }
}