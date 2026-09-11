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

            AddLoadingScreen(builder);

            return builder;
        }

        private static void AddLoadingScreen(IScopeBuilder builder)
        {
            var platformOptions = InternalAssets.OptionsContainer.PlatformOptions;

            // В вебе экран загрузки живёт в index.html: он же закрывает скачивание билда.
            if (platformOptions.IsEditor == false)
            {
                switch (platformOptions.PlatformType)
                {
                    case PlatformType.Website:
                    case PlatformType.ItchIO:
                        builder.Register<WebLoadingScreen>()
                               .As<ILoadingScreen>();
                        return;
                }
            }

            var loadingScreen = builder.Instantiate(GlobalPrefabs.LoadingScreen);

            builder.Inject(loadingScreen);

            builder.RegisterInstance(loadingScreen)
                   .As<ILoadingScreen>()
                   .As<IScopeSetup>();
        }
    }
}