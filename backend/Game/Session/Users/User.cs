using Common;
using Shared;

namespace Game;

public interface IUser
{
    Guid Id { get; }
    int Index { get; }
    ILifetime Lifetime { get; }
    IConnectionReader Reader { get; }
    IConnectionWriter Writer { get; }
    ICommandDispatcher Dispatcher { get; }
}

public class User : IUser
{
    public required Guid Id { get; init; }
    public required int Index { get; init; }
    public required ILifetime Lifetime { get; init; }
    public required IConnectionReader Reader { get; init; }
    public required IConnectionWriter Writer { get; init; }
    public required ICommandDispatcher Dispatcher { get; init; }
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