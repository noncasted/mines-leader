using System;
using Internal;

namespace Game.Session
{
    // The place of a user in the session, kept across its connections. The index, the entity id range and the
    // entities of the user (IEntity.Owner) belong to the seat; an IUser is one connection of it. A connection lost in a
    // session with a reconnect window (SessionCreateOptions.ReconnectWindow) leaves the seat away for it (the entities
    // wait); the user that enters again
    // (ISession.TryAccept) gets the same seat. The seat ends when the user leaves (Leave, a lobby connection closed) or
    // its window expires - its entities end with it. Runs in the session execution queue only.
    public interface ISessionSeat
    {
        // The meta id of the user and its id in this session (IUser.BackendId / IUser.Id).
        Guid UserId { get; }
        Guid Id { get; }
        int Index { get; }
        IReadOnlyLifetime Lifetime { get; }

        // The current connection; null while the seat is away (or before its first connection).
        IUser Connected { get; }

        // The last counter of the id range used by an entity of this seat (SharedEntityIds), 0 - none.
        int EntityCounter { get; }

        // The seat has had a connection before the current one.
        bool IsReturn { get; }

        // The current connection has sent SharedSessionPlayer.Leave: its end is a leave, not a loss.
        bool IsLeaving { get; }

        void OnEntityCreated(int entityId);
        void MarkLeaving(IUser user);
    }
}
