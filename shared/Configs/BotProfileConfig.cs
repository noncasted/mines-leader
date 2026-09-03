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
        /// Пауза между разыгрыванием карт, чтобы игрок успел разглядеть, что бот выложил.
        /// </summary>
        public float CardPlayDelay { get; set; } = 4f;

        /// <summary>
        /// Пауза после всех действий бота перед завершением хода.
        /// Если раунд заканчивается раньше — ожидание обрывается вместе с раундом.
        /// </summary>
        public float EndTurnDelay { get; set; } = 4f;

        public List<BotDeck> Decks { get; set; } = new();
    }
}
