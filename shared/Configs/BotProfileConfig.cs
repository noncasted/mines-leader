using System.Collections.Generic;

namespace Shared
{
    public class BotProfileConfig
    {
        public float ActionDelay { get; set; } = 0.3f;
        public float MinRoundTime { get; set; } = 7f;
        public float MaxRoundTime { get; set; } = 12f;
        public int FlagsPerRound { get; set; } = 3;
        public int CellsOpenPerRound { get; set; } = 3;
        public int CardsUsePerRound { get; set; } = 4;
        public int DeckSize { get; set; } = 6;
        public List<BotDeck> Decks { get; set; } = new();
    }
}
