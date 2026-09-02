using System;
using Shared;

namespace GamePlay.Loop
{
    public enum MatchResultType
    {
        Win = 1,
        Lose = 2,
        Leave = 3
    }

    public class MatchCompletedData
    {
        public MatchResultType Type { get; set; }
        public TimeSpan Duration { get; set; }
        public int RatingChange { get; set; }
        public int CurrentRating { get; set; }

        /// <summary>Статистика локального игрока за матч. Пустая, если матч закончился выходом.</summary>
        public IUserStatsState Stats { get; set; } = new MatchPlayerStats();
    }
}