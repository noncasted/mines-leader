using System.Collections.Generic;
using Internal;

namespace Common.Network
{
    public class NetworkEntity : INetworkEntity
    {
        public NetworkEntity(
            INetworkSender sender,
            INetworkEntityDestroyer destroyer,
            INetworkUser owner,
            int id,
            INetworkEntityProperties properties)
        {
            _destroyer = destroyer;
            _properties = properties;
            Id = id;
            Owner = owner;
            _lifetime = owner.Lifetime.Child();
            Events = new NetworkEvents(sender, this);

            properties.Construct(this);
        }

        private readonly INetworkEntityDestroyer _destroyer;
        private readonly INetworkEntityProperties _properties;
        private readonly ILifetime _lifetime;

        public int Id { get; }
        public INetworkUser Owner { get; }
        public IReadOnlyList<INetworkProperty> Properties => _properties.Entries;
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
    }
}