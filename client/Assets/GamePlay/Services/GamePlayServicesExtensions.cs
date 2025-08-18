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

            builder.RegisterCommand<SnapshotReceiver>()
                .As<ISnapshotReceiver>()
                .As<IScopeSetup>();

            return builder;
        }
    }
}