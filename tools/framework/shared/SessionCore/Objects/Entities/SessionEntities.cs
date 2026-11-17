using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Session
{
    public interface ISessionEntities
    {
        IReadOnlyDictionary<int, IEntity> Entries { get; }
        IReadOnlyDictionary<ISessionSeat, IReadOnlyList<IEntity>> ByUser { get; }

        void Add(IEntity entity);
    }

    public class SessionEntities : ISessionEntities
    {
        private readonly Dictionary<int, IEntity> _entries = new();
        private readonly Dictionary<ISessionSeat, IReadOnlyList<IEntity>> _byUser = new();

        public IReadOnlyDictionary<int, IEntity> Entries => _entries;
        public IReadOnlyDictionary<ISessionSeat, IReadOnlyList<IEntity>> ByUser => _byUser;

        public void Add(IEntity entity)
        {
            _entries.Add(entity.Id, entity);
            entity.Lifetime.Listen(() => _entries.Remove(entity.Id));

            // Session entities have no user.
            if (entity.Owner == null)
                return;

            var owner = entity.Owner;

            if (_byUser.TryGetValue(owner, out var byUser) == false)
            {
                byUser = new List<IEntity>();
                _byUser[owner] = byUser;

                // The entities of a seat end with it: an ended seat is forgotten, not kept with an empty list.
                owner.Lifetime.Listen(() => _byUser.Remove(owner));
            }

            var userEntities = ((List<IEntity>)byUser);
            userEntities.Add(entity);
            entity.Lifetime.Listen(() => userEntities.Remove(entity));
        }
    }
}
