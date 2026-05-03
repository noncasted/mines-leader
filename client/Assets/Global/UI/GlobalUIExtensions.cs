using Internal;
using Tools.PrefabBuilder;

namespace Global.UI
{
    public static class GlobalUIExtensions
    {
        public static void AddUI(this IScopeBuilder builder)
        {
            builder.Register<UIStateMachine>()
                   .WithScopeLifetime()
                   .As<IUIStateMachine>();

            var loadingScreen = builder.Instantiate(Prefabs.LoadingScreen.As<LoadingScreen>());

            builder.Inject(loadingScreen);

            builder.RegisterInstance(loadingScreen)
                   .As<ILoadingScreen>()
                   .As<IScopeSetup>();
        }
    }
}