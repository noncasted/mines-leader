using System;
using Shared;

namespace Meta
{
    // One row of the match history. Rows come in a batch on request, without reactive updates: the history changes
    // only between visits of the profile screen.
    public class ProfileMatchEntry
    {
        public ProfileMatchEntry(
            Guid id,
            GameMatchType type,
            string opponentName,
            bool won,
            DateTime date,
            TimeSpan time)
        {
            Id = id;
            Type = type;
            OpponentName = opponentName;
            Won = won;
            Date = date;
            Time = time;
        }

        public Guid Id { get; }
        public GameMatchType Type { get; }
        public string OpponentName { get; }
        public bool Won { get; }
        public DateTime Date { get; }
        public TimeSpan Time { get; }
    }
}
