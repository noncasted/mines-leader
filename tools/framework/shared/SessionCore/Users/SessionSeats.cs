using System;
using System.Collections.Generic;
using System.Linq;
using Internal;
using Shared;

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

    public class SessionSeat : ISessionSeat
    {
        public SessionSeat(Guid userId, Guid id, int index, IReadOnlyLifetime parentLifetime)
        {
            UserId = userId;
            Id = id;
            Index = index;
            _lifetime = parentLifetime.Child();
        }

        private readonly ILifetime _lifetime;
        private int _connections;

        public Guid UserId { get; }
        public Guid Id { get; }
        public int Index { get; }
        public IReadOnlyLifetime Lifetime => _lifetime;
        public IUser Connected { get; private set; }
        public int EntityCounter { get; private set; }
        public bool IsReturn => _connections > 1;
        public bool IsLeaving { get; private set; }

        public void OnEntityCreated(int entityId)
        {
            if (SharedEntityIds.IsInRangeOf(entityId, Index) == false)
                throw new InvalidOperationException($"Entity {entityId} is out of the id range of seat {Index}");

            var counter = entityId % SharedEntityIds.Stride;

            if (counter > EntityCounter)
                EntityCounter = counter;
        }

        public void MarkLeaving(IUser user)
        {
            if (Connected != user)
                throw new InvalidOperationException($"User {Index} {UserId} leaves through a connection that is not current");

            IsLeaving = true;
        }

        public void Attach(IUser user)
        {
            if (_lifetime.IsTerminated == true)
                throw new InvalidOperationException($"Seat {Index} {UserId} is over: a connection can't take it");

            Connected = user;
            IsLeaving = false;
            _connections++;
        }

        public bool Detach(IUser user)
        {
            if (Connected != user)
                return false;

            Connected = null;
            return true;
        }

        public void End()
        {
            _lifetime.Terminate();
        }
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
