namespace GamePlay.Loop
{
    public class GameOptions
    {
        public int Mines { get; } = 40;
        public int StartCards { get; } = 5;
        public int RequiredCardsInHand { get; } = 5;
        public int DeckSize { get; } = 15;
        public int MaxMana { get; } = 10;
        public int StartMana { get; } = 1;
        public int ManaGain { get; } = 2;
        public int Health { get; } = 10;
        public int Turns { get; } = 5;
        public int RoundTime { get; } = 30;
    }
}