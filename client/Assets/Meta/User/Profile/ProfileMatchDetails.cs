using System;
using System.Collections.Generic;
using Shared;

namespace Meta
{
    // An expanded match: the decks of both sides, the time and the rating change.
    public class ProfileMatchDetails
    {
        public ProfileMatchDetails(
            Guid id,
            GameMatchType type,
            string opponentName,
            bool won,
            TimeSpan time,
            int ratingChange,
            IReadOnlyList<CardType> ownCards,
            IReadOnlyList<CardType> opponentCards)
        {
            Id = id;
            Type = type;
            OpponentName = opponentName;
            Won = won;
            Time = time;
            RatingChange = ratingChange;
            OwnCards = ownCards;
            OpponentCards = opponentCards;
        }

        public Guid Id { get; }
        public GameMatchType Type { get; }
        public string OpponentName { get; }
        public bool Won { get; }
        public TimeSpan Time { get; }
        public int RatingChange { get; }
        public IReadOnlyList<CardType> OwnCards { get; }
        public IReadOnlyList<CardType> OpponentCards { get; }
    }
}
