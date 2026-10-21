using Internal;
#if UNITY_WEBGL && !UNITY_EDITOR
using Global.Web;
#endif

namespace Global.UI
{
    public static class GlobalUIExtensions
    {
        public static IScopeBuilder AddGlobalUI(this IScopeBuilder builder)
        {
            builder.AddUI();

            // On the web the loading screen lives in index.html: it also covers the build download.
            // The choice is made with #if, not if: the container generator builds all installer branches at once,
            // and a prefab or an alternative not registered at runtime breaks the scope creation.
#if UNITY_WEBGL && !UNITY_EDITOR
            builder.AddWebLoadingScreen();
#else
            builder.AddLoadingScreen(GlobalPrefabs.LoadingScreen);
#endif

            builder.Register<ClientOutdatedNotice>()
                   .WithScopeLifetime()
                   .As<IClientOutdatedNotice>();

            return builder;
        }
    }
}
