using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Session
{
    public interface ISessionSeats
    {
        IReadOnlyDictionary<Guid, ISessionSeat> Entries { get; }

        // A new connection of the user: its live seat, or a new one with the next index.
        ISessionSeat Take(Guid userId);

        // The same with the index given by the host (the session of a P2P host keeps the indices of the server session
        // of the match): a live seat of the user must have it, a new one takes it.
        ISessionSeat Take(Guid userId, int index);

        // The connection created over the seat (IUser.Seat) becomes its current one; an older one is replaced.
        void Attach(IUser user);

        // The connection has ended. True when it was the current one of its seat (the seat has no connection now);
        // false for a connection replaced by a newer one of the same seat.
        bool Detach(IUser user);

        // The user has left or its reconnect window has expired.
        void End(ISessionSeat seat);
    }

    public class SessionSeats : ISessionSeats
    {
        public SessionSeats(ISessionUsers users, ISessionData data)
        {
            _users = users;
            _data = data;
        }

        private readonly ISessionUsers _users;
        private readonly ISessionData _data;
        private readonly Dictionary<Guid, ISessionSeat> _entries = new();

        // The session id of a meta user is the same for every seat of it in this session (a seat taken again after the
        // previous one has ended).
        private readonly Dictionary<Guid, Guid> _sessionIds = new();

        public IReadOnlyDictionary<Guid, ISessionSeat> Entries => _entries;

        public ISessionSeat Take(Guid userId)
        {
            if (_entries.TryGetValue(userId, out var existing) == true)
                return existing;

            if (_sessionIds.TryGetValue(userId, out var id) == false)
            {
                id = Guid.NewGuid();
                _sessionIds.Add(userId, id);
            }

            return Create(userId, id, _users.GetNextIndex());
        }

        public ISessionSeat Take(Guid userId, int index)
        {
            if (_entries.TryGetValue(userId, out var existing) == true)
            {
                if (existing.Index != index)
                    throw new InvalidOperationException($"User {userId} takes index {index}, its seat has {existing.Index}");

                return existing;
            }

            if (_entries.Values.Any(seat => seat.Index == index) == true)
                throw new InvalidOperationException($"Index {index} of user {userId} is taken by another seat");

            if (_sessionIds.TryGetValue(userId, out var id) == false)
            {
                id = Guid.NewGuid();
                _sessionIds.Add(userId, id);
            }

            return Create(userId, id, index);
        }

        private ISessionSeat Create(Guid userId, Guid id, int index)
        {
            var created = new SessionSeat(userId, id, index, _data.Lifetime);
            _entries.Add(userId, created);
            created.Lifetime.Listen(() => _entries.Remove(userId));

            return created;
        }

        public void Attach(IUser user)
        {
            if (_entries.TryGetValue(user.BackendId, out var seat) == false || seat != user.Seat)
                throw new InvalidOperationException($"User {user.Index} {user.BackendId} attaches to a seat that is not its live one");

            ((SessionSeat)seat).Attach(user);
        }

        // The seat may be over already (the session has ended).
        public bool Detach(IUser user)
        {
            return ((SessionSeat)user.Seat).Detach(user);
        }

        public void End(ISessionSeat seat)
        {
            if (_entries.TryGetValue(seat.UserId, out var current) == false || current != seat)
                throw new InvalidOperationException($"Seat {seat.Index} {seat.UserId} is not a live seat of the session");

            if (seat.Connected != null)
                throw new InvalidOperationException($"Seat {seat.Index} {seat.UserId} is ended while it has a connection");

            ((SessionSeat)seat).End();
        }
    }
}
