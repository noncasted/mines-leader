namespace Shared
{
    [SharedGrainState(Table = "configs", State = "rating_config", Key = GrainKeyType.String, Lookup = "RatingConfig")]
    public class RatingOptions
    {
        public int WinRating { get; set; } = 25;
        public int LossRating { get; set; } = 15;
    }
}