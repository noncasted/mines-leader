using Common;
using Shared;

namespace Game;

public interface IUser
{
    Guid Id { get; }
    int Index { get; }
    ILifetime Lifetime { get; }
    IChannelReader Reader { get; }
    IChannelWriter Writer { get; }
}

public static class UserExtensions
{
    public static void Send(this IUser user, INetworkContext context)
    {
        user.Writer.WriteEmpty(context);
    }

    public static void Send(this IUser user, INetworkContext context, int requestId)
    {
        user.Writer.WriteFull(context, requestId);
    }
}