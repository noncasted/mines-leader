namespace Shared
{
    [SharedGrainState(Table = "configs", State = "game_mode_config", Key = GrainKeyType.String,
        Lookup = "GameModeConfig")]
    public class GameModeOptions
    {
        public LastManStandingModeOptions LastManStanding { get; set; } = new();
        public TimeLimitedModeOptions TimeLimited { get; set; } = new();

        public int GetCardMovesCost(GameMatchType type)
        {
            return type switch
            {
                GameMatchType.TimeLimited => TimeLimited.CardMovesCost,
                _ => LastManStanding.CardMovesCost,
            };
        }
    }

    public class LastManStandingModeOptions
    {
        public int PlayerHealth { get; set; } = 3;
        public int PlayerMoves { get; set; } = 5;
        public int PlayerStartMana { get; set; } = 1;
        public int MaxManaCap { get; set; } = 10;
        public int RoundTime { get; set; } = 30;
        public int CardMovesCost { get; set; } = 1;
    }

    public class TimeLimitedModeOptions
    {
        public int PlayerHealth { get; set; } = 3;
        public int PlayerMoves { get; set; } = 5;
        public int PlayerStartMana { get; set; } = 1;
        public int MaxManaCap { get; set; } = 10;
        public int RoundTime { get; set; } = 120;
        public int TimeGainPerAction { get; set; } = 5;
        public int CardMovesCost { get; set; } = 1;
    }
}