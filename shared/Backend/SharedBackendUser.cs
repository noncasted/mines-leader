using System;
using System.Collections.Generic;
using MemoryPack;

// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace Shared
{
    public partial class SharedBackendUser
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

        [MemoryPackable]
        public partial class Match : INetworkContext
        {
            public Guid Id { get; set; }
            public List<Guid> Participants { get; set; }
            public DateTime Date { get; set; }
            public Guid Winner { get; set; }
            public TimeSpan Time { get; set; }
            public GameMatchType Type { get; set; }
        }

        public static IUnionBuilder<INetworkContext> Register(IUnionBuilder<INetworkContext> builder)
        {
            return builder
                .Add<ProfileProjection>()
                .Add<ProgressionProjection>()
                .Add<UpdateDeckRequest>()
                .Add<DeckProjection>()
                .Add<Match>();
        }
    }
}