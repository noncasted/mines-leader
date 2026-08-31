using Internal;

namespace Global.Systems
{
    public static class GlobalSystemExtensions
    {
        public static IScopeBuilder AddSystemUtils(this IScopeBuilder builder)
        {
            builder.Register<ApplicationProxy>()
                   .As<IScreen>()
                   .As<IApplicationFlow>();

            var updaterPrefab = Prefabs.Global.GlobalUpdater.GetComponent<Updater>();
            var updater = builder.Instantiate(updaterPrefab);

            builder.RegisterComponent(updater)
                   .As<IUpdater>()
                   .AsSelfResolvable();

            builder.Register<DelayRunner>()
                   .As<IDelayRunner>();

            return builder;
        }
    }
}
