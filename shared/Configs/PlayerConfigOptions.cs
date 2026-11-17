namespace Shared
{
    [SharedGrainState(Table = "configs", State = "player_config_mode_config", Key = GrainKeyType.String,
        Lookup = "PlayerConfig")]
    public class PlayerConfigOptions
    {
        public int HandSize { get; set; } = 5;
        public int DeckSize { get; set; } = 10;
    }
}