using System.Collections.Generic;

namespace Common.Network
{
    public interface INetworkEntitiesCollection
    {
        IReadOnlyDictionary<int, INetworkEntity> Entries { get; }
        
        void Add(INetworkEntity entity);
    }
    
    public class NetworkEntityCollection : INetworkEntitiesCollection
    {
        private readonly Dictionary<int, INetworkEntity> _entries = new();
        
        public IReadOnlyDictionary<int, INetworkEntity> Entries => _entries;
        
        public void Add(INetworkEntity entity)
        {
            _entries.Add(entity.Id, entity);
            
            entity.Lifetime.Listen(() => _entries.Remove(entity.Id));
        }
    }
}