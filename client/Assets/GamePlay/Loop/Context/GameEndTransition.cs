using Meta;

namespace GamePlay.Loop
{
    public interface IGameEndTransition
    {
    }

    public class GameEndTransition
    {
        public class Exit : IGameEndTransition
        {
        }

        public class Rematch : IGameEndTransition
        {
            public SessionData NewSession { get; set; }
        }
    }
}