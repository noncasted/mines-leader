using System.Collections.Generic;

namespace Shared
{
    public class BotProfileConfig
    {
        public float ActionDelay { get; set; } = 0.3f;
        public float MinRoundTime { get; set; } = 7f;
        public float MaxRoundTime { get; set; } = 12f;
        public int FlagsPerRound { get; set; } = 3;

        /// <summary>
        /// Pause between card plays, so the player can see what the bot has put down.
        /// </summary>
        public float CardPlayDelay { get; set; } = 4f;

        /// <summary>
        /// Pause after all bot actions before ending the turn.
        /// If the round ends earlier, the wait is cut off together with the round.
        /// </summary>
        public float EndTurnDelay { get; set; } = 4f;

        public List<BotDeck> Decks { get; set; } = new();
    }
}
