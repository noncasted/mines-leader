namespace GamePlay.Loop
{
    public enum GameResultType
    {
        Win = 1,
        Lose = 2,
        Leave = 3
    }

    public class GameResult
    {
        public GameResultType Type { get; set; }
    }
}