using System.Collections.Generic;
using VContainer;
using VContainer.Internal;

namespace Common.Network
{
    public interface INetworkObjectProperties
    {
        IReadOnlyDictionary<int, INetworkProperty> Entries { get; }

        void Add(INetworkProperty property);
    }
    
    public class NetworkObjectProperties : INetworkObjectProperties
    {
        [Inject]
        public NetworkObjectProperties(ContainerLocal<IReadOnlyList<INetworkProperty>> properties)
        {
            foreach (var property in properties.Value)
                Add(property);
        }

        public NetworkObjectProperties()
        {
        }

        private int _index;

        private readonly Dictionary<int, INetworkProperty> _entries = new();

        public IReadOnlyDictionary<int, INetworkProperty> Entries => _entries;

        public void Add(INetworkProperty property)
        {
            _index++;
            _entries.Add(_index, property);
        }
    }
}