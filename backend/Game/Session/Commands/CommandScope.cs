using Shared;

namespace Game;

public class CommandScope
{
    public required ISessionUsers Users { get; init; }
    public required IUser User { get; init; }
}

public static class CommandScopeExtensions
{
    public static void SendAllExceptSelf(this CommandScope scope, INetworkContext context)
    {
        foreach (var user in scope.Users)
        {
            if (user == scope.User)
                continue;

            user.Send(context);
        }
    }
}