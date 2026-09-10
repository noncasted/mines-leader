using Internal;

namespace Global.Setup
{
    public static class GlobalUpdaterExtensions
    {
        public static IScopeBuilder AddUpdater(this IScopeBuilder builder)
        {
            var updater = builder.Instantiate(GlobalPrefabs.GlobalUpdater);

            builder.RegisterComponent(updater)
                   .As<IUpdater>()
                   .AsSelfResolvable();

            builder.Register<DelayRunner>()
                   .As<IDelayRunner>();

            return builder;
        }
    }
}