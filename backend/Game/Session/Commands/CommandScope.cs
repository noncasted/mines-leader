using Shared;

namespace Game;

public class CommandScope
{
    public required ISession Session { get; init; }
    public required IUser User { get; init; }
}

public static class CommandScopeExtensions
{
    public static void SendAllExceptSelf(this CommandScope scope, INetworkContext context)
    {
        foreach (var user in scope.Session.Users)
        {
            if (user == scope.User)
                continue;

            user.Send(context);
        }
    }
}