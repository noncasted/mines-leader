using Internal;

namespace Global.UI
{
    public static class GlobalUIExtensions
    {
        public static void AddUI(this IScopeBuilder builder)
        {
            builder.Register<UIStateMachine>()
                .WithScopeLifetime()
                .As<IUIStateMachine>();
            
            var loadingScreenOptions = builder.GetAsset<LoadingScreenOptions>();
            var loadingScreen = builder.Instantiate(loadingScreenOptions.Prefab);

            builder.Inject(loadingScreen);
            
            builder.RegisterInstance(loadingScreen)
                .As<ILoadingScreen>()
                .As<IScopeSetup>();
        }
    }
}