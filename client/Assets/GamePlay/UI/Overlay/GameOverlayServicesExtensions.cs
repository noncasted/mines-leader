using GamePlay.Loop;
using Internal;

namespace GamePlay.UI
{
    public static class GameOverlayServicesExtensions
    {
        public static IScopeBuilder AddGameOverlayServices(this IScopeBuilder builder)
        {
            builder.Register<GameOverlay>()
                   .As<IScopeBaseSetup>()
                   .As<IGameOverlay>();

            builder.Register<GameInfoOverlay>()
                   .As<IScopeBaseSetup>()
                   .As<IGameInfoOverlay>();

            builder.Register<RoundSkipButton>()
                   .As<IGameStarted>();

            builder.Register<RoundTimer>()
                   .As<IGameStarted>();

            builder.Register<BoardMinesCounterView>()
                   .As<IGameStarted>();

            builder.Register<CardDropTarget>()
                   .As<ICardDropTarget>()
                   .As<IScopeSetup>();
            
            return builder;
        }
    }
}