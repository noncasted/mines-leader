using Game.Session;
using Shared;

namespace Game.GamePlay;

public class RematchRequestCommand : Command<RematchContexts.Request>
{
    public RematchRequestCommand(IRematchAwaiter rematchAwaiter)
    {
        _rematchAwaiter = rematchAwaiter;
    }

    private readonly IRematchAwaiter _rematchAwaiter;

    protected override void Execute(IUser user, RematchContexts.Request context)
    {
        _rematchAwaiter.OnRematchAccepted();
    }
}