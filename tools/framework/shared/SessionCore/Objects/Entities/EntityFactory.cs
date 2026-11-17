using System;
using System.Collections.Generic;
using System.Linq;
using Shared;

namespace Game.Session
{
    public interface IEntityFactory
    {
        // An entity of the backend owned by the seat (EntityBuilder).
        EntityBuilder Create(ISessionSeat owner);

        IEntity Create(SharedSessionEntity.CreateRequest request, IUser owner);
    }

    public class EntityFactory : IEntityFactory
    {
        public EntityFactory(
            ISessionData session,
            ISessionUsers users,
            ISessionEntities entities,
            ISessionObjects objects,
            IPropertyUpdateSender propertyUpdateSender)
        {
            _session = session;
            _users = users;
            _entities = entities;
            _objects = objects;
            _propertyUpdateSender = propertyUpdateSender;
        }

        private readonly ISessionData _session;
        private readonly ISessionUsers _users;
        private readonly ISessionEntities _entities;
        private readonly ISessionObjects _objects;
        private readonly IPropertyUpdateSender _propertyUpdateSender;

        private int _backendCounter;

        public EntityBuilder Create(ISessionSeat owner)
        {
            _backendCounter++;
            var id = SharedEntityIds.CreateBackend(_backendCounter);

            return new EntityBuilder(_users, _entities, _objects, _propertyUpdateSender, owner, id);
        }

        public IEntity Create(SharedSessionEntity.CreateRequest request, IUser owner)
        {
            var properties = new Dictionary<int, IObjectProperty>();

            foreach (var propertyRequest in request.Properties)
            {
                var property = new ObjectProperty(propertyRequest.PropertyId, propertyRequest.Value);
                property.Construct(_propertyUpdateSender, request.Id);
                properties.Add(propertyRequest.PropertyId, property);
            }

            // A session entity outlives its creator: it belongs to the session.
            var entity = request.Payload is ISessionEntityPayload
                ? new Entity(null, _session.Lifetime, properties, request.Id, request.Payload)
                : new Entity(owner.Seat, owner.Seat.Lifetime, properties, request.Id, request.Payload);

            _entities.Add(entity);
            _objects.Add(entity);

            return entity;
        }
    }
}
