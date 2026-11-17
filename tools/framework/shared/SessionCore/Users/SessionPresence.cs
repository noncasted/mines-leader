using System.Linq;
using Shared;

namespace Game.Session
{
    // What the clients are told about the users of the session: who they are when they join, who is away, who is gone.
    // Runs in the session queue of the host (the users, the seats and the entities live there).
    public interface ISessionPresence
    {
        // A connection is handled now: its client learns its seat (the entities of the seat to restore), the other
        // users learn it and it learns them (the seats away too), and it gets every entity of the session.
        void Introduce(IUser user);

        // The connection of the seat is lost and the seat waits for its return: the others see it away.
        void MarkAway(ISessionSeat seat);

        // The user is gone for good: the others destroy its entities with it (the host ends them with the seat).
        void EndSeat(ISessionSeat seat);
    }

    public class SessionPresence : ISessionPresence
    {
        public SessionPresence(ISessionUsers users, ISessionSeats seats, ISessionEntities entities)
        {
            _users = users;
            _seats = seats;
            _entities = entities;
        }

        private readonly ISessionUsers _users;
        private readonly ISessionSeats _seats;
        private readonly ISessionEntities _entities;

        public void Introduce(IUser user)
        {
            // The entities of the seat to restore: the ones its client has created (the ones built by the host come
            // to it as remote ones).
            user.Send(new SharedSessionPlayer.LocalUpdate()
            {
                Index = user.Index,
                Id = user.Id,
                EntityCounter = user.Seat.EntityCounter,
                IsReturn = user.Seat.IsReturn,
                SeatEntities = _entities.Entries.Values.Count(entity =>
                    entity.Owner == user.Seat && SharedEntityIds.IsBackendIssued(entity.Id) == false)
            });

            _users.IterateOthers(user, other => {
                // An older connection of a seat (this one's own or another one's, replaced by a return) is being
                // dropped: the peers are the current connections of the other seats.
                if (other.Seat == user.Seat || other.Seat.Connected != other)
                    return;

                // Peers know each other by the index and the id in this session only: the meta id of a user is its
                // login.
                user.Send(new SharedSessionPlayer.RemoteUpdate()
                {
                    Index = other.Index,
                    Id = other.Id
                });

                other.Send(new SharedSessionPlayer.RemoteUpdate()
                {
                    Index = user.Index,
                    Id = user.Id
                });
            });

            // A seat that waits for its return is known as away: the overview below has its entities, and an owner the
            // client does not know fails them.
            foreach (var seat in _seats.Entries.Values)
            {
                if (seat == user.Seat || seat.Connected != null)
                    continue;

                user.Send(new SharedSessionPlayer.RemoteUpdate()
                {
                    Index = seat.Index,
                    Id = seat.Id
                });

                user.Send(new SharedSessionPlayer.RemoteAway()
                {
                    Index = seat.Index
                });
            }

            foreach (var entity in _entities.Entries.Values)
                user.Send(entity.CreateOverview());
        }

        public void MarkAway(ISessionSeat seat)
        {
            _users.SendAll(new SharedSessionPlayer.RemoteAway()
            {
                Index = seat.Index
            });
        }

        public void EndSeat(ISessionSeat seat)
        {
            _users.SendAll(new SharedSessionPlayer.RemoteDisconnect()
            {
                Index = seat.Index
            });

            _seats.End(seat);
        }
    }
}
