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

        [MemoryPackable]
        public partial class RatingProjection : INetworkContext
        {
            public int Rating { get; set; }
        }

        [MemoryPackable]
        public partial class CardsProjection : INetworkContext
        {
            public List<CardType> OwnedCards { get; set; } = new();
        }

        [MemoryPackable]
        public partial class LootProjection : INetworkContext
        {
            public int AwardedCount { get; set; }
            public List<LootEntry> Boxes { get; set; } = new();

            [MemoryPackable]
            public partial class LootEntry
            {
                public Guid Id { get; set; }
            }
        }

        [MemoryPackable]
        public partial class LootOpenRequest : INetworkContext
        {
            public Guid LootBoxId { get; set; }
        }

        [MemoryPackable]
        public partial class LootOpenResponse : INetworkContext
        {
            public Guid LootBoxId { get; set; }
            public List<CardType> Choices { get; set; } = new();
        }

        [MemoryPackable]
        public partial class LootChooseRequest : INetworkContext
        {
            public Guid LootBoxId { get; set; }
            public CardType ChosenCard { get; set; }
        }

        [MemoryPackable]
        public partial class MatchHistoryRequest : INetworkContext
        {
            public int Count { get; set; }
        }

        [MemoryPackable]
        public partial class MatchHistoryResponse : INetworkContext
        {
            public List<Match> Matches { get; set; } = new();
        }

        [MemoryPackable]
        public partial class MatchDetailsRequest : INetworkContext
        {
            public Guid MatchId { get; set; }
        }

        [MemoryPackable]
        public partial class MatchDetailsResponse : INetworkContext
        {
            public Guid MatchId { get; set; }
            public List<CardType> OpponentCards { get; set; } = new();
            public List<CardType> OwnCards { get; set; } = new();
            public TimeSpan Time { get; set; }
            public int RatingChange { get; set; }
            public int ProgressionChange { get; set; }
            public bool Won { get; set; }
        }

        public static IUnionBuilder<INetworkContext> Register(IUnionBuilder<INetworkContext> builder)
        {
            return builder
                   .Add<ProfileProjection>()
                   .Add<ProgressionProjection>()
                   .Add<UpdateDeckRequest>()
                   .Add<DeckProjection>()
                   .Add<Match>()
                   .Add<RatingProjection>()
                   .Add<CardsProjection>()
                   .Add<LootProjection>()
                   .Add<LootOpenRequest>()
                   .Add<LootOpenResponse>()
                   .Add<LootChooseRequest>()
                   .Add<MatchHistoryRequest>()
                   .Add<MatchHistoryResponse>()
                   .Add<MatchDetailsRequest>()
                   .Add<MatchDetailsResponse>();
        }
    }
}