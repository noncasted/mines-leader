using System;
using System.Collections.Generic;
using System.Linq;
using Shared;

namespace Game.Session
{
    // An entity built by the backend itself (a player of a match), owned by a seat: its id is issued by the backend
    // (SharedEntityIds.CreateBackend), so the client of the owner receives it as any remote entity. It lives while its
    // seat does and is sent to every user on Build.
    public class EntityBuilder
    {
        public EntityBuilder(
            ISessionUsers users,
            ISessionEntities entities,
            ISessionObjects objects,
            IPropertyUpdateSender propertyUpdateSender,
            ISessionSeat owner,
            int id)
        {
            _users = users;
            _entities = entities;
            _objects = objects;
            _propertyUpdateSender = propertyUpdateSender;
            _owner = owner;
            _id = id;
        }

        private readonly ISessionUsers _users;
        private readonly ISessionEntities _entities;
        private readonly ISessionObjects _objects;
        private readonly IPropertyUpdateSender _propertyUpdateSender;
        private readonly ISessionSeat _owner;
        private readonly Dictionary<int, IObjectProperty> _properties = new();
        private readonly int _id;

        private IEntityPayload _payload;

        public EntityBuilder WithProperty(ObjectProperty property)
        {
            property.Construct(_propertyUpdateSender, _id);
            _properties.Add(property.Id, property);
            return this;
        }

        public ValueProperty<T> AddProperty<T>(int id) where T : new()
        {
            var property = new ValueProperty<T>(id);
            property.Construct(_propertyUpdateSender, _id);
            _properties.Add(id, property);
            return property;
        }

        public EntityBuilder WithPayload(IEntityPayload payload)
        {
            _payload = payload;
            return this;
        }

        public IEntity Build()
        {
            if (_payload == null)
                throw new InvalidOperationException($"Entity {_id} of seat {_owner.Index} is built without a payload");

            var entity = new Entity(_owner, _owner.Lifetime, _properties, _id, _payload);

            _entities.Add(entity);
            _objects.Add(entity);

            _users.SendAll(entity.CreateOverview());

            return entity;
        }
    }
}
