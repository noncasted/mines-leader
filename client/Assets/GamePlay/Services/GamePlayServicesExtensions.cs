using GamePlay.Agent;
using Internal;
using Network;

namespace GamePlay.Services
{
    public static class GamePlayServicesExtensions
    {
        public static IScopeBuilder AddGamePlayServices(this IScopeBuilder builder)
        {
            builder.LoadSpriteGroup(Sprites.GameUI);
            builder.LoadPrefabGroup(Prefabs.GamePlay);
            
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