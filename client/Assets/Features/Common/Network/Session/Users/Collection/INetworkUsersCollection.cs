using System.Collections.Generic;

namespace Common.Network
{
    public interface INetworkUsersCollection
    {
        IReadOnlyDictionary<int, INetworkUser> Entries { get; }        
        INetworkUser Local { get; }
        
        void Add(INetworkUser user);
    }
}