using System;
using System.Collections.Generic;
using MemoryPack;

// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace Shared
{
    public partial class BackendUserContexts
    {

        [MemoryPackable]
        public partial class ProfileProjection : INetworkContext
        {
            public Guid Id { get; set; }
            public string Name { get; set; } = string.Empty;
        }

        [MemoryPackable]
        public partial class ProgressionProjection : INetworkContext
        {
            public int Experience { get; set; }
        }

        [MemoryPackable]
        public partial class UpdateDeckRequest : INetworkContext
        {
            public DeckProjection Projection { get; set; }
        }

        [MemoryPackable]
        public partial class DeckProjection : INetworkContext
        {
            public Dictionary<int, Entry> Entries { get; set; }
            public int SelectedIndex { get; set; }

            [MemoryPackable]
            public partial class Entry
            {
                public int DeckIndex { get; set; }
                public IReadOnlyList<CardType> Cards { get; set; }
            }
        }
    }
}