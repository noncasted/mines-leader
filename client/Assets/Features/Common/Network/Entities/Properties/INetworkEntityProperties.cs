using System.Collections.Generic;

namespace Common.Network
{
    public interface INetworkEntityProperties
    {
        IReadOnlyList<INetworkProperty> Entries { get; }

        void Construct(INetworkEntity entity);
        void Add(INetworkProperty property);
    }
}