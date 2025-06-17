using System.Collections.Generic;

namespace Common.Network.Common
{
    public interface INetworkObjectsCollection
    {
        IReadOnlyDictionary<int, INetworkObject> Entries { get; }

        void Add(INetworkObject networkObject);
    }
    
    public class NetworkObjectsCollection : INetworkObjectsCollection
    {
        private readonly Dictionary<int, INetworkObject> _entries = new();

        public IReadOnlyDictionary<int, INetworkObject> Entries => _entries;

        public void Add(INetworkObject networkObject)
        {
            _entries.Add(networkObject.Id, networkObject);
            networkObject.Lifetime.Listen(() => _entries.Remove(networkObject.Id));
        }
    }
}