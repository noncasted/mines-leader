using Global.Backend;
using Internal;

namespace GamePlay.Services
{
    public static class GamePlayServicesExtensions
    {
        public static IScopeBuilder AddGamePlayServices(this IScopeBuilder builder)
        {
            builder.Register<GameInput>()
                .As<IGameInput>()
                .As<IScopeSetup>();

            builder.Register<SnapshotReceiver>()
                .As<INetworkCommand>();

            return builder;
        }
    }
}