using Game.Session;
using Shared;

namespace Game.GamePlay;

// A command of the cheats window and the development tools. Runs only when ICheatsAccess allows it; otherwise the
// request is answered with ErrorResponse Forbidden (the session runner logs it) and the refusal goes to the game log
// of the session. Nothing of the request is executed.
public abstract class CheatCommand<TRequest> : GameCommand<TRequest> where TRequest : INetworkContext
{
    protected CheatCommand(GameCommandUtils utils, ICheatsAccess access) : base(utils)
    {
        _access = access;
    }

    private readonly ICheatsAccess _access;

    protected override EmptyResponse Execute(IUser user, TRequest request)
    {
        if (_access.IsAllowed == false)
        {
            var reason = _access.RefusalReason;
            Utils.SessionLogger.Log($"[Cheat] Refused {ErrorResponse.Describe(request)} of user {user.Id}: {reason}");
            throw new SessionRequestException(Shared.ErrorCode.Forbidden, $"Cheats are disabled: {reason}");
        }

        return base.Execute(user, request);
    }
}
