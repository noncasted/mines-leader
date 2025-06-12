using System.Collections.Generic;
using Internal;

namespace Common.Network.Common
{
    public interface INetworkObject
    {
        int Id { get; }
        IReadOnlyDictionary<int, INetworkProperty> Properties { get; }
        INetworkEvents Events { get; }
        IReadOnlyLifetime Lifetime { get; }
    }
}