using Common;

namespace Shared
{
    [SharedGrainState(Table = "configs",
        State = "bot_config",
        Lookup = "BotConfig",
        Key = GrainKeyType.String)]
    public class BotConfigOptions
    {
        public float MatchmakingApplyThreshold { get; set; } = 20f;
        public float ActionDelay { get; set; } = 0.3f;
        public float MinRoundTime { get; set; } = 7f;
        public float MaxRoundTime { get; set; } = 12f;
        public int FlagsPerRound { get; set; } = 3;
        public int CellsOpenPerRound { get; set; } = 3;
        public int CardsUsePerRound { get; set; } = 4;
    }
}