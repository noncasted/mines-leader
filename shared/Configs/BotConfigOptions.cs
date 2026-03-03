namespace Shared
{
    public class BotConfigOptions
    {
        public float MatchmakingApplyThreshold { get; set; } = 20f;
        public float ActionDelay { get; set; } = 0.3f;
        public int FlagsPerRound { get; set; } = 3;
        public int CellsOpenPerRound { get; set; } = 3;
        public int CardsUsePerRound { get; set; } = 2;
    }
}