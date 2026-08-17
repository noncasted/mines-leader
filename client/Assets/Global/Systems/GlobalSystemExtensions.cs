using Internal;
using Tools;

namespace Global.Systems
{
    public static class GlobalSystemExtensions
    {
        public static IScopeBuilder AddSystemUtils(this IScopeBuilder builder)
        {
            builder.Register<ApplicationProxy>()
                   .As<IScreen>()
                   .As<IApplicationFlow>();

            var updaterPrefab = Prefabs.GlobalUpdater.As<Updater>();
            var updater = builder.Instantiate(updaterPrefab);

            builder.RegisterComponent(updater)
                   .As<IUpdater>()
                   .AsSelfResolvable();

            builder.Register<DelayRunner>()
                   .As<IDelayRunner>();

            return builder;
        }
    }

    [PrefabDefinition]
    public static class GlobalUpdaterPrefab
    {
        public static void Define(PrefabBuilder builder)
        {
            builder
                .WithName("Global/GlobalUpdater")
                .WithComponent<Updater>();
        }
    }
}