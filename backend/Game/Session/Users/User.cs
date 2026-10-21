using Common.Network;
using Common.Reactive;
using Shared;

namespace Game.Session;

public interface IUser
{
    // The id of the user in this session: a new one in every session, the one the game and its clients know (player
    // ids, snapshots). BackendId is the meta id (the login of the user, S3): only for the meta (decks, the match
    // grain, ratings) and the logs, never sent to the clients.
    Guid Id { get; }
    Guid BackendId { get; }
    int Index { get; }
    bool IsBot { get; }
    ILifetime Lifetime { get; }
    IConnection Connection { get; }
    ICommandDispatcher Dispatcher { get; }
}

public class User : IUser
{
    public required Guid Id { get; init; }
    public required Guid BackendId { get; init; }
    public required int Index { get; init; }
    public required bool IsBot { get; init; }
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