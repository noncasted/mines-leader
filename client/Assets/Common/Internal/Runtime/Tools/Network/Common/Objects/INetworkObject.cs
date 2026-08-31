using System.Collections.Generic;

namespace Internal
{
    public interface INetworkObject
    {
        int Id { get; }
        IReadOnlyDictionary<int, INetworkProperty> Properties { get; }
        INetworkEvents Events { get; }
        IReadOnlyLifetime Lifetime { get; }
    }
}