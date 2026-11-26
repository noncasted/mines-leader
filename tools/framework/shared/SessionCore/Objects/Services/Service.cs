using System;
using System.Collections.Generic;
using Internal;
using Shared;

namespace Game.Session
{
    public interface IService : IObject
    {
        string Key { get; }

        void Setup(IReadOnlyLifetime lifetime);

        // The user fetched the service (GetRequest): from now on it gets the property updates of the service
        // (pushed by the service itself or written by other clients), until it leaves.
        void AddSubscriber(IUser user);

        IReadOnlyList<IUser> Subscribers { get; }

        SharedSessionService.Overview CreateOverview();
    }

    public class Service : IService
    {
        public Service(string key)
        {
            Key = key;
            Id = GetId(key);
        }

        private readonly Dictionary<int, IObjectProperty> _properties = new();
        private readonly List<IUser> _subscribers = new();

        private IReadOnlyLifetime _lifetime = new TerminatedLifetime();

        public string Key { get; }
        public IReadOnlyDictionary<int, IObjectProperty> Properties => _properties;
        public int Id { get; }
        public IReadOnlyLifetime Lifetime => _lifetime;
        public IReadOnlyList<IUser> Subscribers => _subscribers;

        // Same on every process (Shared.StableHash, not string.GetHashCode) and always negative: entity ids are
        // non-negative (user index * stride + counter), so a service never takes the id of an entity in SessionObjects.
        // Two keys with one hash are refused by SessionObjects.Add when the second service is added.
        public static int GetId(string key)
        {
            return Shared.StableHash.Get(key) | int.MinValue;
        }

        // Updates go only to the users that fetched the service: a client that has not requested it yet doesn't
        // know its object id (it gets the current values in the GetResponse instead). Updates from clients - the
        // same, in SetPropertyCommand.
        public void Setup(IReadOnlyLifetime lifetime)
        {
            _lifetime = lifetime;
            var sender = new SubscribersUpdateSender(_subscribers);

            foreach (var (_, property) in _properties)
                property.Construct(sender, Id);

            OnStarted(lifetime);
        }

        public void AddSubscriber(IUser user)
        {
            if (_subscribers.Contains(user))
                return;

            _subscribers.Add(user);
            user.Lifetime.Listen(() => _subscribers.Remove(user));
        }

        public void BindProperty(IObjectProperty property)
        {
            if (_properties.ContainsKey(property.Id))
                throw new InvalidOperationException($"Property with index {property.Id} already exists.");

            _properties.Add(property.Id, property);
        }

        public SharedSessionService.Overview CreateOverview()
        {
            var properties = new List<SharedSessionObject.PropertyUpdate>();

            foreach (var (_, property) in Properties)
            {
                properties.Add(new SharedSessionObject.PropertyUpdate()
                {
                    PropertyId = property.Id,
                    Value = property.RawValue,
                    Version = property.Version
                });
            }

            var updatedContext = new SharedSessionService.Overview()
            {
                Key = Key,
                Properties = properties
            };

            return updatedContext;
        }

        protected virtual void OnStarted(IReadOnlyLifetime lifetime)
        {
        }
    }
}
