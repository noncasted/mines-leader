namespace Shared
{
    public class GameOptions
    {
        public int StartMana { get; init; } = 5;
        public int MaxMana { get; init; } = 10;
        
        public int MaxHealth { get; init; } = 10;
        public int StartHealth { get; init; } = 10;

        public int DeckSize { get; init; } = 10;

        public int HandSize { get; init; } = 5;
        public int RoundTime { get; init; } = 30;
        public int MovesCount { get; init; } = 5;
    }
}