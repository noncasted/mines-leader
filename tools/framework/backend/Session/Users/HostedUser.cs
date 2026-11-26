using Common.Network;
using Internal;
using Shared;

namespace Game.Session;

// A user of the backend session: the core user (IUser) over a socket of the gateway (or a bot connection), with the
// dispatcher that runs its messages in the session queue.
public interface IHostedUser : IUser
{
    // The gateway that accepted the socket returns only after Socket.Closed: the lifetime of the user ends before the
    // close frame is out.
    IConnection Socket { get; }

    ICommandDispatcher Dispatcher { get; }
}

public class HostedUser : IHostedUser
{
    public HostedUser(ISessionSeat seat, bool isBot, ILifetime lifetime, IConnection socket, ICommandDispatcher dispatcher)
    {
        Seat = seat;
        IsBot = isBot;
        Lifetime = lifetime;
        Socket = socket;
        Dispatcher = dispatcher;
        Connection = new SocketUserConnection(socket);
    }

    public ISessionSeat Seat { get; }
    public Guid Id => Seat.Id;
    public Guid BackendId => Seat.UserId;
    public int Index => Seat.Index;
    public bool IsBot { get; }
    public ILifetime Lifetime { get; }
    public IUserConnection Connection { get; }
    public IConnection Socket { get; }
    public ICommandDispatcher Dispatcher { get; }
}
