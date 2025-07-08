namespace GamePlay.Loop
{
    public enum GameResultType
    {
        Win,
        Lose, 
        Leave
    }

    public class GameResult
    {
        public GameResultType Type { get; set; }
    }
}