using Common.Network;
using Common.Reactive;
using Shared;

namespace Game.Session;

public interface IUser
{
    Guid Id { get; }
    int Index { get; }
    ILifetime Lifetime { get; }
    IConnection Connection { get; }
    ICommandDispatcher Dispatcher { get; }
}

public class User : IUser
{
    public required Guid Id { get; init; }
    public required int Index { get; init; }
    public required ILifetime Lifetime { get; init; }
    public required IConnection Connection { get; init; }
    public required ICommandDispatcher Dispatcher { get; init; }
}

public static class UserExtensions
{
    extension(IUser user)
    {
        public void Send(INetworkContext context)
        {
            user.Connection.Writer.WriteOneWay(context);
        }

        public void Send(INetworkContext context, int requestId)
        {
            user.Connection.Writer.WriteResponse(context, requestId);
        }
    }
}