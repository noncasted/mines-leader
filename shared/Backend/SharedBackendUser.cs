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
        public partial class UserStatsProjection : INetworkContext, IUserStatsState
        {
            public Dictionary<UserStatType, long> Counters { get; set; } = new();
            public Dictionary<CardGroup, long> CardsPlayedByGroup { get; set; } = new();

            public long Get(UserStatType type)
            {
                if (Counters == null)
                    return 0;

                return Counters.TryGetValue(type, out var value) ? value : 0;
            }

            public long GetCardsPlayed(CardGroup group)
            {
                if (CardsPlayedByGroup == null)
                    return 0;

                return CardsPlayedByGroup.TryGetValue(group, out var value) ? value : 0;
            }
        }

        [MemoryPackable]
        public partial class InGameAchievementsProjection : INetworkContext
        {
            public List<UnlockedAchievement> Unlocked { get; set; } = new();

            [MemoryPackable]
            public partial class UnlockedAchievement
            {
                public InGameAchievementType Type { get; set; }
                public int Tier { get; set; }
                public DateTime Date { get; set; }

                /// <summary>Награда уже выбрана игроком, ачивку больше нельзя открыть.</summary>
                public bool Claimed { get; set; }

                public CardType? RewardCard { get; set; }
            }
        }

        [MemoryPackable]
        public partial class AchievementRewardOptionsRequest : INetworkContext
        {
            public InGameAchievementType Type { get; set; }
            public int Tier { get; set; }
        }

        [MemoryPackable]
        public partial class AchievementRewardOptionsResponse : INetworkContext
        {
            public InGameAchievementType Type { get; set; }
            public int Tier { get; set; }
            public List<CardType> Options { get; set; } = new();
        }

        [MemoryPackable]
        public partial class ClaimAchievementRewardRequest : INetworkContext
        {
            public InGameAchievementType Type { get; set; }
            public int Tier { get; set; }
            public CardType Card { get; set; }
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

            /// <summary>Заполняется под запрашивающего игрока: имя второго участника матча.</summary>
            public string OpponentName { get; set; } = string.Empty;
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
            public bool Won { get; set; }
            public string OpponentName { get; set; } = string.Empty;
            public DateTime Date { get; set; }
            public GameMatchType Type { get; set; }
        }

        public static IUnionBuilder<INetworkContext> Register(IUnionBuilder<INetworkContext> builder)
        {
            return builder
                   .Add<ProfileProjection>()
                   .Add<UserStatsProjection>()
                   .Add<InGameAchievementsProjection>()
                   .Add<AchievementRewardOptionsRequest>()
                   .Add<AchievementRewardOptionsResponse>()
                   .Add<ClaimAchievementRewardRequest>()
                   .Add<UpdateDeckRequest>()
                   .Add<DeckProjection>()
                   .Add<Match>()
                   .Add<RatingProjection>()
                   .Add<CardsProjection>()
                   .Add<MatchHistoryRequest>()
                   .Add<MatchHistoryResponse>()
                   .Add<MatchDetailsRequest>()
                   .Add<MatchDetailsResponse>();
        }
    }
}