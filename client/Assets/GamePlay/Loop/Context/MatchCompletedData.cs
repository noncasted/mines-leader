using System;
using System.Collections.Generic;
using Shared;

namespace GamePlay.Loop
{
    public class MatchCompletedData
    {
        public MatchCompletedData(
            MatchResultType type,
            TimeSpan duration,
            int ratingChange,
            int currentRating,
            IUserStatsState stats,
            Guid loserId,
            IReadOnlyList<BoardRevealState> boards)
        {
            Type = type;
            Duration = duration;
            RatingChange = ratingChange;
            CurrentRating = currentRating;
            Stats = stats;
            LoserId = loserId;
            Boards = boards;
        }

        public MatchResultType Type { get; }
        public TimeSpan Duration { get; }
        public int RatingChange { get; }
        public int CurrentRating { get; }

        // Stats of the local player in the match. Empty when the match ended without results.
        public IUserStatsState Stats { get; }

        public Guid LoserId { get; }

        // Server states of both boards at the end of the match. Empty when the match ended without results.
        public IReadOnlyList<BoardRevealState> Boards { get; }

        // A match that ended without results (a leave, a broken sync): no duration, rating, stats or boards.
        public static MatchCompletedData WithoutResults(MatchResultType type)
        {
            return new MatchCompletedData(
                type: type,
                duration: TimeSpan.Zero,
                ratingChange: 0,
                currentRating: 0,
                stats: new MatchPlayerStats(),
                loserId: Guid.Empty,
                boards: Array.Empty<BoardRevealState>());
        }
    }
}