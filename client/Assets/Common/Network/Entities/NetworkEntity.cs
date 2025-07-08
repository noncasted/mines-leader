using System.Collections.Generic;
using System.Linq;
using Global.Backend;
using Internal;
using VContainer.Internal;

namespace Common.Network
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
            INetworkSocket socket,
            INetworkEntityDestroyer destroyer,
            INetworkUser owner,
            int id,
            ContainerLocal<IReadOnlyList<INetworkProperty>> properties)
        {
            _destroyer = destroyer;
            Id = id;
            Owner = owner;
            _lifetime = owner.Lifetime.Child();
            Events = new NetworkEvents(socket, this);

            Properties = properties.Value.ToDictionary(t => t.Id);
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
    }
}