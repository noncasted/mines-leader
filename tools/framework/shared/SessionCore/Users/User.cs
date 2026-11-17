using System;
using Internal;
using Shared;

namespace Game.Session
{
    // One connection of a user to the session. The place of the user (index, entities) is its seat, kept across
    // connections: Index is the index of the seat.
    public interface IUser
    {
        // The id of the user in this session (of its seat): a new one in every session, the one the game and its
        // clients know (SharedSessionPlayer.LocalUpdate / RemoteUpdate). BackendId is the meta id - the login of the
        // user: only for the meta and the logs, never sent to the clients.
        Guid Id { get; }
        Guid BackendId { get; }
        int Index { get; }

        // A user played by the host: no client, its connection lives as long as the session and it never holds the
        // session open (ISessionAdmission of the backend does not count it).
        bool IsBot { get; }

        ISessionSeat Seat { get; }
        ILifetime Lifetime { get; }
        IUserConnection Connection { get; }
    }

    // What the session does with the connection of a user: the transport is the host's (a WebSocket of the backend,
    // a data channel or a loopback of a host client).
    public interface IUserConnection
    {
        void Send(INetworkContext context);
        void Respond(INetworkContext context, int requestId);

        // The session drops the connection (a stale one of a user that came anew): its end removes the user.
        void Disconnect();
    }

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
