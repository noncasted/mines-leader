using Shared;

namespace Game.Session
{
    public static class UserExtensions
    {
        public static void Send(this IUser user, INetworkContext context)
        {
            user.Connection.Send(context);
        }

        public static void Send(this IUser user, INetworkContext context, int requestId)
        {
            user.Connection.Respond(context, requestId);
        }
    }
}
