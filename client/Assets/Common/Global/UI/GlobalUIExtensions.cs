using Internal;

namespace Global.UI
{
    public static class GlobalUIExtensions
    {
        public static IScopeBuilder AddUI(this IScopeBuilder builder)
        {
            builder.Register<UIStateMachine>()
                   .WithScopeLifetime()
                   .As<IUIStateMachine>();

            var loadingScreen = builder.Instantiate(GlobalPrefabs.LoadingScreen);

            builder.Inject(loadingScreen);

            builder.RegisterInstance(loadingScreen)
                   .As<ILoadingScreen>()
                   .As<IScopeSetup>();

            return builder;
        }
    }
}