namespace Shared
{
    public class GameOptions
    {
        public int StartMana { get; init; }
        public int MaxMana { get; init; }
        
        public int MaxHealth { get; init; }
        public int StartHealth { get; init; }
        
        public int DeckSize { get; init; }
        public int StartCards { get; init; }
        
        public int HandSize { get; init; }
        public int RoundTime { get; init; }
        public int MovesCount { get; init; }
    }
}