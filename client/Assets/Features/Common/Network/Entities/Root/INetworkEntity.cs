using System.Collections.Generic;
using Internal;

namespace Common.Network
{
    public interface INetworkEntity
    { 
        int Id { get; }
        INetworkUser Owner { get; }
        IReadOnlyList<INetworkProperty> Properties { get; }
        IReadOnlyLifetime Lifetime { get; }
        INetworkEvents Events { get; }
        
        void Destroy();
        void DestroyRemote();
    }
}