using System.Collections.Generic;
using VContainer;
using VContainer.Internal;

namespace Common.Network
{
    public class NetworkEntityProperties : INetworkEntityProperties
    {
        [Inject]
        public NetworkEntityProperties(ContainerLocal<IReadOnlyList<INetworkProperty>> properties)
        {
            _entries.AddRange(properties.Value);
        }

        public NetworkEntityProperties()
        {
        }

        private readonly List<INetworkProperty> _entries = new();

        private INetworkEntity _entity;

        public IReadOnlyList<INetworkProperty> Entries => _entries;

        public void Construct(INetworkEntity entity)
        {
            _entity = entity;

            foreach (var entry in _entries)
                entry.Construct(_entity);
        }

        public void Add(INetworkProperty property)
        {
            property.Construct(_entity);
            _entries.Add(property);
        }
    }
}