using System;

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
    }
}