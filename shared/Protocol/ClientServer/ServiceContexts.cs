using System.Collections.Generic;
using MemoryPack;

namespace Shared
{
    public partial class ServiceContexts
    {
        [MemoryPackable]
        public partial class GetRequest : INetworkContext
        {
            public string Key { get; set; }
            public IReadOnlyList<int> PropertiesIds { get; set; }
        }

        [MemoryPackable]
        public partial class GetResponse : INetworkContext
        {
            public string Key { get; set; }
            public int Id { get; set; }
            public IReadOnlyList<ObjectContexts.PropertyUpdate> Properties { get; set; }
        }

        [MemoryPackable]
        public partial class Overview : INetworkContext
        {
            public string Key { get; set; }
            public IReadOnlyList<ObjectContexts.PropertyUpdate> Properties { get; set; }
        }
    }
}