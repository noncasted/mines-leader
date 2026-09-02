using Internal;

namespace GamePlay.Services
{
    public static class GamePlayServicesExtensions
    {
        public static IScopeBuilder AddGamePlayServices(this IScopeBuilder builder)
        {
            builder.RequestSpriteGroup(Sprites.GameUI);
            builder.RequestPrefabGroup(GamePlayPrefabs.Group);
            
            builder.Register<GameInput>()
                   .As<IGameInput>()
                   .As<IScopeSetup>();

            builder.RegisterCommand<SnapshotReceiver>()
                   .As<ISnapshotReceiver>()
                   .As<IScopeSetup>();

            builder.RegisterCommand<AgentObservationHandler>();

            return builder;
        }
    }
}