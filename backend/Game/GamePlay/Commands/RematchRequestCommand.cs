using Shared;

namespace Game.GamePlay;

public class RematchRequestCommand : Command<MatchActionContexts.RequestRematch>
{
    public RematchRequestCommand(IRematchAwaiter rematchAwaiter)
    {
        _rematchAwaiter = rematchAwaiter;
    }

    private readonly IRematchAwaiter _rematchAwaiter;

    protected override void Execute(IUser user, MatchActionContexts.RequestRematch context)
    {
        _rematchAwaiter.OnRematchAccepted(user.Id);
    }
}