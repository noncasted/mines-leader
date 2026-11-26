using System;
using Internal;
using Shared;

namespace Game.Session
{
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
}
