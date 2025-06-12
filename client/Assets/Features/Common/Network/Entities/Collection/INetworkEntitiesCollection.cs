using System.Collections.Generic;

namespace Common.Network
{
    public interface INetworkEntitiesCollection
    {
        IReadOnlyDictionary<int, INetworkEntity> Entries { get; }
        
        void Add(INetworkEntity entity);
    }
}