namespace Shared
{
    public class GameOptions
    {
        public int StartMana { get; set; } = 5;
        public int MaxMana { get; set; } = 10;
        
        public int MaxHealth { get; set; } = 10;
        public int StartHealth { get; set; } = 10;

        public int DeckSize { get; set; } = 10;

        public int HandSize { get; set; } = 5;
        public int RoundTime { get; set; } = 30;
        public int MovesCount { get; set; } = 5;
    }
}