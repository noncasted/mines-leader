using GamePlay.Agent;
using GamePlay.Prefabs;
using Internal;
using Network;

namespace GamePlay.Services
{
    public static class GamePlayServicesExtensions
    {
        public static IScopeBuilder AddGamePlayServices(this IScopeBuilder builder)
        {
            builder.Register<GameInput>()
                   .As<IGameInput>()
                   .As<IScopeSetup>();

            builder.RegisterCommand<SnapshotReceiver>()
                   .As<ISnapshotReceiver>()
                   .As<IScopeSetup>();

            builder.RegisterCommand<AgentObservationHandler>();

            builder.RegisterAsset<GamePrefabs>();

            return builder;
        }
    }
}