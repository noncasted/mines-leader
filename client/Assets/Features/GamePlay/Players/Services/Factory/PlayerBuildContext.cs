using GamePlay.Loop;
using Internal;

namespace GamePlay.Players
{
    public class PlayerBuildContext
    {
        public PlayerBuildContext(IGameContext gameContext, IEntityBuilder builder)
        {
            GameContext = gameContext;
            Builder = builder;
        }

        public IGameContext GameContext { get; }
        public IEntityBuilder Builder { get; }
    }
}