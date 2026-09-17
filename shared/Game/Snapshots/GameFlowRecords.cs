using System;
using System.Collections.Generic;
using MemoryPack;

namespace Shared
{
    [MemoryPackable]
    public partial class GameCompletedRecord : IMoveSnapshotRecord
    {
        public Guid Winner { get; set; }

        /// <summary>Длительность матча — от старта сессии до победы.</summary>
        public TimeSpan Duration { get; set; }

        /// <summary>Итоги каждого участника: рейтинг и внутриигровая статистика за матч.</summary>
        public List<MatchPlayerResult> Players { get; set; } = new();

        /// <summary>
        /// Полные состояния обоих полей на момент конца матча, включая мины. Клиент
        /// синхронизирует по ним доски и подрывает мины проигравшего перед экраном результатов.
        /// </summary>
        public List<BoardRevealState> Boards { get; set; } = new();
    }

    [MemoryPackable]
    public partial class BoardRevealState
    {
        public Guid OwnerId { get; set; }
        public List<CellRevealState> Cells { get; set; } = new();
    }

    [MemoryPackable]
    public partial class CellRevealState
    {
        public Position Position { get; set; }
        public bool IsFree { get; set; }
        public int MinesAround { get; set; }
        public bool IsFlagged { get; set; }
        public bool HasMine { get; set; }
    }

    /// <summary>Итоги одного игрока за матч. Собираются на сессии, уезжают на клиент в момент завершения.</summary>
    [MemoryPackable]
    public partial class MatchPlayerResult
    {
        public Guid PlayerId { get; set; }

        /// <summary>Изменение рейтинга за этот матч, со знаком.</summary>
        public int RatingChange { get; set; }

        /// <summary>Рейтинг игрока уже с учётом <see cref="RatingChange"/>.</summary>
        public int Rating { get; set; }

        public MatchPlayerStats Stats { get; set; } = new();
    }

    /// <summary>Срез статистики одного игрока за матч.</summary>
    [MemoryPackable]
    public partial class MatchPlayerStats : IUserStatsState
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
    public partial class TimeLimitedRoundRecord : IMoveSnapshotRecord
    {
        public Guid CurrentPlayer { get; set; }
        public Dictionary<Guid, long> SecondsLeft { get; set; }
    }

    [MemoryPackable]
    public partial class LastManStandingRoundRecord : IMoveSnapshotRecord
    {
        public Guid CurrentPlayer { get; set; }
        public int CurrentRound { get; set; }
        public int SecondsLeft { get; set; }
    }
}
