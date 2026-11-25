using System;
using System.Collections.Generic;
using MemoryPack;

namespace Shared
{
    [MemoryPackable]
    public partial class GameCompletedRecord : IMoveSnapshotRecord
    {
        public Guid Winner { get; set; }

        /// <summary>Match duration - from the session start to the victory.</summary>
        public TimeSpan Duration { get; set; }

        /// <summary>Results of every participant: rating and in-game statistics of the match.</summary>
        public List<MatchPlayerResult> Players { get; set; } = new();

        /// <summary>
        /// Full states of both boards at the end of the match, mines included. The client
        /// syncs the boards by them and blows up the loser's mines before the results screen.
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

    /// <summary>Results of one player for the match. Collected on the session, sent to the client at the finish.</summary>
    [MemoryPackable]
    public partial class MatchPlayerResult
    {
        public Guid PlayerId { get; set; }

        /// <summary>Rating change for this match, signed.</summary>
        public int RatingChange { get; set; }

        /// <summary>Player rating already including <see cref="RatingChange"/>.</summary>
        public int Rating { get; set; }

        public MatchPlayerStats Stats { get; set; } = new();
    }

    /// <summary>Statistics slice of one player for the match.</summary>
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
