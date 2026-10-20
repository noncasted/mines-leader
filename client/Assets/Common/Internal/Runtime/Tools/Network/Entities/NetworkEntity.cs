using System;
using System.Collections.Generic;

namespace Internal
{
    public interface INetworkEntity : INetworkObject
    {
        INetworkUser Owner { get; }

        void Destroy();
        void DestroyRemote();
    }

    public class NetworkEntity : INetworkEntity
    {
        public NetworkEntity(
            INetworkConnection connection,
            INetworkEntityDestroyer destroyer,
            INetworkUser owner,
            int id,
            IReadOnlyList<INetworkProperty> properties)
        {
            _destroyer = destroyer;
            Id = id;
            Owner = owner;
            _lifetime = owner.Lifetime.Child();
            Events = new NetworkEvents(connection, this);

            Properties = CollectProperties(properties);
        }

        private readonly INetworkEntityDestroyer _destroyer;
        private readonly ILifetime _lifetime;

        public int Id { get; }
        public INetworkUser Owner { get; }
        public IReadOnlyDictionary<int, INetworkProperty> Properties { get; }
        public IReadOnlyLifetime Lifetime => _lifetime;
        public INetworkEvents Events { get; }

        public void Destroy()
        {
            _lifetime.Terminate();
            _destroyer.Destroy(this);
        }

        public void DestroyRemote()
        {
            _lifetime.Terminate();
        }

        // Two value types with the same StableHash of the name would silently share one id on the wire.
        private static IReadOnlyDictionary<int, INetworkProperty> CollectProperties(
            IReadOnlyList<INetworkProperty> properties)
        {
            var result = new Dictionary<int, INetworkProperty>(properties.Count);

            foreach (var property in properties)
            {
                if (result.TryGetValue(property.Id, out var existing) == true)
                {
                    throw new InvalidOperationException(
                        $"Network entity has two properties with id {property.Id}: {existing.GetType()} and {property.GetType()}");
                }

                result.Add(property.Id, property);
            }

            return result;
        }
    }
}