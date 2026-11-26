using System;
using Internal;

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
}
